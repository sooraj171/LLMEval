using System.Net;
using System.Text;
using LLMEval;
using Microsoft.Extensions.DependencyInjection;

namespace LLMEval.Tests;

public class FluentEvalTests
{
    [Fact]
    public async Task Eval_Direct_Exact_PassesAndAsserts()
    {
        var result = await Eval.Direct()
            .Exact(actual: "Paris", expected: "Paris")
            .WithThreshold(1.0)
            .EvaluateAsync();

        result.ShouldPass().ShouldScoreAbove(0.9);
        Assert.Equal(1.0, result.Score);
    }

    [Fact]
    public async Task Eval_Direct_Exact_ShouldPass_ThrowsWhenFailed()
    {
        var result = await Eval.Direct()
            .Exact(actual: "London", expected: "Paris")
            .WithThreshold(1.0)
            .EvaluateAsync();

        Assert.Throws<LLMEvalAssertionException>(() => result.ShouldPass());
    }

    [Fact]
    public async Task Eval_Direct_Keyword_Passes()
    {
        var result = await Eval.Direct()
            .Keyword(actual: "The capital is Paris in France", expected: "Paris France")
            .WithThreshold(0.5)
            .EvaluateAsync();

        result.ShouldPass();
        Assert.True(result.Score >= 0.5);
    }

    [Fact]
    public async Task Eval_Direct_Semantic_ReturnsScoreAndDetails()
    {
        var result = await Eval.Direct()
            .Semantic(actual: "Paris is the capital of France", expected: "Paris")
            .WithThreshold(0.1)
            .EvaluateAsync();

        Assert.True(result.Score > 0);
        Assert.False(string.IsNullOrEmpty(result.Details));
        result.ShouldPass();
    }

    [Fact]
    public async Task Eval_Judge_UsesInjectedService()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            """{"choices":[{"message":{"content":"0.95 looks good","role":"assistant"}}]}"""
        };
        var service = new AdvancedEvaluationService(factory);

        var result = await Eval.Judge()
            .Using(service)
            .WithQuestion("Capital?")
            .WithResponse("Paris")
            .WithExpected("Paris")
            .WithProvider(ProviderType.OpenAI)
            .WithEndpoint("https://api.openai.com/v1/chat/completions")
            .WithThreshold(0.8)
            .EvaluateAsync();

        result.ShouldPass();
        Assert.Equal(0.95, result.Score);
        Assert.Contains("looks good", result.Details);
    }

    [Fact]
    public async Task Eval_Grounding_UsesInjectedService_AndShouldBeGrounded()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            """{"response":"SUPPORTED","done":true}"""
        };
        var service = new AdvancedEvaluationService(factory);

        var result = await Eval.Grounding()
            .Using(service)
            .WithQuestion("What?")
            .WithResponse("One claim.")
            .WithExpected("Reference supports the claim.")
            .WithProvider(ProviderType.Ollama)
            .WithEndpoint("http://localhost")
            .WithModel("test")
            .WithThreshold(0.5)
            .EvaluateAsync();

        result.ShouldPass().ShouldBeGrounded();
        Assert.Equal("Low", result.RiskLevel);
    }

    [Fact]
    public async Task Eval_WithOptions_AppliesApiKeyAndModel()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            """{"choices":[{"message":{"content":"1.0 perfect","role":"assistant"}}]}"""
        };
        var service = new AdvancedEvaluationService(factory);
        var options = new LLMEvalOptions
        {
            DefaultProvider = ProviderType.AzureOpenAI,
            Endpoint = "https://example.openai.azure.com",
            ApiKey = "secret",
            Model = "deploy-1",
            DefaultPassThreshold = 0.7
        };

        var result = await Eval.Judge()
            .Using(service)
            .WithOptions(options)
            .WithQuestion("Q")
            .WithResponse("A")
            .WithExpected("E")
            .WithProvider(ProviderType.AzureOpenAI)
            .EvaluateAsync();

        result.ShouldPass();
        Assert.Equal(1.0, result.Score);
    }
}

public class AssertionTests
{
    [Fact]
    public void ShouldBeGrounded_ThrowsOnHighRisk()
    {
        var result = new EvaluationResult
        {
            Score = 0.5,
            IsPassed = false,
            RiskLevel = "High",
            UnsupportedStatements = new[] { "Hallucination" },
            Details = "bad"
        };

        var ex = Assert.Throws<LLMEvalAssertionException>(() => result.ShouldBeGrounded());
        Assert.Same(result, ex.Result);
        Assert.Contains("Hallucination", ex.Message);
    }

    [Fact]
    public void ShouldBeGrounded_PassesWhenLowRiskAndNoUnsupported()
    {
        var result = new EvaluationResult
        {
            Score = 1.0,
            IsPassed = true,
            RiskLevel = "Low",
            UnsupportedStatements = Array.Empty<string>(),
            Details = "ok"
        };

        Assert.Same(result, result.ShouldBeGrounded());
    }

    [Fact]
    public void ShouldScoreAbove_ThrowsWhenNotAbove()
    {
        var result = new EvaluationResult { Score = 0.5, IsPassed = false, Details = "mid" };
        var ex = Assert.Throws<LLMEvalAssertionException>(() => result.ShouldScoreAbove(0.5));
        Assert.Contains("0.5", ex.Message);
    }

    [Fact]
    public void ShouldPass_ThrowsIncludesDetails()
    {
        var result = new EvaluationResult { Score = 0, IsPassed = false, Details = "reason-xyz" };
        var ex = Assert.Throws<LLMEvalAssertionException>(() => result.ShouldPass());
        Assert.Contains("reason-xyz", ex.Message);
    }
}

public class ModelNameMappingTests
{
    [Fact]
    public void ApplyModelNameToConfiguration_SetsModelWhenMissing()
    {
        var request = new EvaluationRequest { ModelName = "gpt-4o-mini" };
        AdvancedEvaluationService.ApplyModelNameToConfiguration(request);
        Assert.Equal("gpt-4o-mini", request.Configuration["Model"]);
    }

    [Fact]
    public void ApplyModelNameToConfiguration_DoesNotOverwriteExisting()
    {
        var request = new EvaluationRequest
        {
            ModelName = "ignored",
            Configuration = new Dictionary<string, string> { ["Model"] = "keep-me" }
        };
        AdvancedEvaluationService.ApplyModelNameToConfiguration(request);
        Assert.Equal("keep-me", request.Configuration["Model"]);
    }

    [Fact]
    public async Task EvaluateAsync_AppliesModelNameIntoConfiguration_ForJudge()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            """{"choices":[{"message":{"content":"0.8 ok","role":"assistant"}}]}"""
        };
        var service = new AdvancedEvaluationService(factory);

        var request = new EvaluationRequest
        {
            Question = "Q",
            AiResponse = "A",
            GoldenOutput = "E",
            ProviderType = ProviderType.OpenAI,
            Endpoint = "https://api.openai.com/v1/chat/completions",
            ModelName = "from-model-name",
            PassThreshold = 0.5,
            EvaluationType = EvaluationType.LLMAsJudge
        };

        var result = await service.EvaluateAsync(request);
        Assert.True(result.IsPassed);
        Assert.Equal("from-model-name", request.Configuration["Model"]);
    }
}

public class OptionsAndServiceDefaultsTests
{
    [Fact]
    public void LLMEvalOptions_ToConfigurationDictionary_IncludesSetValues()
    {
        var options = new LLMEvalOptions
        {
            ApiKey = "k",
            Model = "m",
            Temperature = "0"
        };

        var dict = options.ToConfigurationDictionary();
        Assert.Equal("k", dict["ApiKey"]);
        Assert.Equal("m", dict["Model"]);
        Assert.Equal("0", dict["Temperature"]);
    }

    [Fact]
    public async Task AdvancedEvaluationService_AppliesOptionsDefaults()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            """{"choices":[{"message":{"content":"0.9 good","role":"assistant"}}]}"""
        };
        var options = new LLMEvalOptions
        {
            Endpoint = "https://api.openai.com/v1/chat/completions",
            ApiKey = "key",
            Model = "gpt-test",
            DefaultPassThreshold = 0.8
        };
        var service = new AdvancedEvaluationService(factory, new HttpClient(), options);

        var result = await service.EvaluateAsync(new EvaluationRequest
        {
            Question = "Q",
            AiResponse = "A",
            GoldenOutput = "E",
            ProviderType = ProviderType.OpenAI,
            EvaluationType = EvaluationType.LLMAsJudge
            // Endpoint / threshold / config intentionally empty — filled from options
        });

        result.ShouldPass();
    }

    [Fact]
    public async Task EvaluateAsync_AzureOpenAI_ParsesLikeOpenAI()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            """{"choices":[{"message":{"content":"0.88 azure-ok","role":"assistant"}}]}"""
        };
        var service = new AdvancedEvaluationService(factory);

        var result = await service.EvaluateAsync(new EvaluationRequest
        {
            Question = "Q",
            AiResponse = "A",
            GoldenOutput = "E",
            ProviderType = ProviderType.AzureOpenAI,
            Endpoint = "https://r.openai.azure.com",
            Configuration = new Dictionary<string, string>
            {
                ["ApiKey"] = "k",
                ["Model"] = "dep"
            },
            PassThreshold = 0.8,
            EvaluationType = EvaluationType.LLMAsJudge
        });

        Assert.Equal(0.88, result.Score);
        Assert.True(result.IsPassed);
        Assert.Contains("azure-ok", result.Details);
    }
}

public class AzureOpenAIProviderTests
{
    [Fact]
    public void BuildChatCompletionsUrl_FromResourceRoot()
    {
        var url = AzureOpenAIProvider.BuildChatCompletionsUrl(
            "https://myresource.openai.azure.com",
            "gpt-4o-mini",
            "2024-02-15-preview");

        Assert.Equal(
            "https://myresource.openai.azure.com/openai/deployments/gpt-4o-mini/chat/completions?api-version=2024-02-15-preview",
            url);
    }

    [Fact]
    public void BuildChatCompletionsUrl_FullUrlWithoutApiVersion_AppendsVersion()
    {
        var url = AzureOpenAIProvider.BuildChatCompletionsUrl(
            "https://myresource.openai.azure.com/openai/deployments/dep/chat/completions",
            "ignored",
            "2024-02-15-preview");

        Assert.Contains("api-version=2024-02-15-preview", url);
        Assert.Contains("/chat/completions", url);
    }

    [Fact]
    public void BuildChatCompletionsUrl_FullUrlWithApiVersion_Unchanged()
    {
        var input = "https://myresource.openai.azure.com/openai/deployments/dep/chat/completions?api-version=2024-06-01";
        var url = AzureOpenAIProvider.BuildChatCompletionsUrl(input, "ignored", "2024-02-15-preview");
        Assert.Equal(input, url);
    }

    [Fact]
    public async Task GetResponseAsync_PostsToAzureAndReturnsBody()
    {
        var handler = new StubHttpHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/openai/deployments/my-deploy/chat/completions", req.RequestUri!.ToString());
            Assert.True(req.Headers.Contains("api-key"));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"role":"assistant","content":"0.9 ok"}}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        });

        using var http = new HttpClient(handler);
        var provider = new AzureOpenAIProvider(http);
        var json = await provider.GetResponseAsync(
            "https://myresource.openai.azure.com",
            "score this",
            new Dictionary<string, string>
            {
                ["ApiKey"] = "test-key",
                ["Model"] = "my-deploy",
                ["Temperature"] = "0"
            });

        Assert.Contains("0.9 ok", json);
    }

    [Fact]
    public async Task GetResponseAsync_MissingEndpoint_Throws()
    {
        using var http = new HttpClient(new StubHttpHandler((_, _) => throw new Exception("should not call")));
        var provider = new AzureOpenAIProvider(http);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetResponseAsync("", "p", new Dictionary<string, string>
            {
                ["ApiKey"] = "k",
                ["Model"] = "m"
            }));
    }

    [Fact]
    public async Task GetResponseAsync_MissingApiKey_Throws()
    {
        using var http = new HttpClient(new StubHttpHandler((_, _) => throw new Exception("should not call")));
        var provider = new AzureOpenAIProvider(http);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetResponseAsync("https://r.openai.azure.com", "p", new Dictionary<string, string>
            {
                ["Model"] = "m"
            }));
    }

    [Fact]
    public async Task GetResponseAsync_MissingDeployment_Throws()
    {
        using var http = new HttpClient(new StubHttpHandler((_, _) => throw new Exception("should not call")));
        var provider = new AzureOpenAIProvider(http);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetResponseAsync("https://r.openai.azure.com", "p", new Dictionary<string, string>
            {
                ["ApiKey"] = "k"
            }));
    }
}

public class EvaluationSuiteTests
{
    [Fact]
    public async Task Suite_RunsJsonDataset_WritesReports()
    {
        var json = """
        [
          { "id": "1", "question": "Capital?", "actual": "Paris", "expected": "Paris", "matchingType": "exact", "threshold": 1.0 },
          { "id": "2", "question": "Capital?", "actual": "London", "expected": "Paris", "matchingType": "exact", "threshold": 1.0 }
        ]
        """;

        var cases = EvaluationSuite.ParseDataset(json);
        Assert.Equal(2, cases.Count);

        var service = new AdvancedEvaluationService(new AiProviderFactory());
        var suite = new EvaluationSuite(service, new LLMEvalOptions { DefaultPassThreshold = 1.0 });
        var result = await suite.RunAsync(cases);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Passed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(0.5, result.PassRate);
        Assert.False(result.MeetsPassRate(0.8));
        Assert.True(result.MeetsPassRate(0.5));

        var dir = Path.Combine(Path.GetTempPath(), "llmeval-suite-" + Guid.NewGuid().ToString("N"));
        try
        {
            await suite.WriteReportsAsync(result, dir);
            Assert.True(File.Exists(Path.Combine(dir, "report.json")));
            Assert.True(File.Exists(Path.Combine(dir, "report.html")));
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "report.html"));
            Assert.Contains("Paris", html);
            Assert.Contains("PASS", html);
            Assert.Contains("FAIL", html);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ParseDataset_Jsonl()
    {
        var text = """
        {"id":"a","actual":"x","expected":"x","matchingType":"exact"}
        {"id":"b","actual":"y","expected":"z","matchingType":"exact"}
        """;
        var cases = EvaluationSuite.ParseDataset(text, "cases.jsonl");
        Assert.Equal(2, cases.Count);
        Assert.Equal("a", cases[0].Id);
    }

    [Fact]
    public void ParseDataset_WrappedCasesObject()
    {
        var json = """
        {
          "cases": [
            { "id": "w1", "actual": "Paris", "expected": "Paris", "matchingType": "exact", "threshold": 1.0 }
          ]
        }
        """;
        var cases = EvaluationSuite.ParseDataset(json);
        Assert.Single(cases);
        Assert.Equal("w1", cases[0].Id);
    }

    [Fact]
    public void ParseDataset_Empty_ReturnsEmpty()
    {
        Assert.Empty(EvaluationSuite.ParseDataset(""));
        Assert.Empty(EvaluationSuite.ParseDataset("   "));
    }

    [Fact]
    public async Task LoadAsync_ReadsFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "llmeval-cases-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, """
                [{ "id": "f1", "actual": "a", "expected": "a", "matchingType": "exact", "threshold": 1.0 }]
                """);
            var cases = await EvaluationSuite.LoadAsync(path);
            Assert.Single(cases);
            Assert.Equal("f1", cases[0].Id);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Suite_HtmlEscapesSpecialCharacters()
    {
        var cases = new[]
        {
            new SuiteCase
            {
                Id = "esc",
                Question = "What is <tag> & \"quote\"?",
                Actual = "A < B",
                Expected = "A < B",
                MatchingType = "exact",
                Threshold = 1.0
            }
        };
        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));
        var report = await suite.RunAsync(cases);
        var dir = Path.Combine(Path.GetTempPath(), "llmeval-esc-" + Guid.NewGuid().ToString("N"));
        try
        {
            await suite.WriteReportsAsync(report, dir);
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "report.html"));
            Assert.Contains("&lt;tag&gt;", html);
            Assert.Contains("&amp;", html);
            Assert.DoesNotContain("<tag>", html);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}

public class DiRegistrationTests
{
    [Fact]
    public void AddLLMEval_ResolvesEvaluationService()
    {
        var services = new ServiceCollection();
        services.AddLLMEval(o =>
        {
            o.DefaultProvider = ProviderType.OpenAI;
            o.DefaultPassThreshold = 0.75;
            o.Model = "gpt-4o-mini";
        });

        using var sp = services.BuildServiceProvider();
        var eval = sp.GetRequiredService<IEvaluationService>();
        Assert.NotNull(eval);
        Assert.IsType<AdvancedEvaluationService>(eval);
        Assert.NotNull(sp.GetRequiredService<IAiProviderFactory>());
        Assert.NotNull(sp.GetRequiredService<IHttpClientFactory>());
    }

    [Fact]
    public void AddLLMEval_WithoutConfigure_StillResolves()
    {
        var services = new ServiceCollection();
        services.AddLLMEval();
        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IEvaluationService>());
    }
}

internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => _handler = handler;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request, cancellationToken);
}
