using System.Text.Json;
using LLMEval;
using Microsoft.Extensions.DependencyInjection;

namespace LLMEval.Tests;

public class MetricPluginTests
{
    [Fact]
    public async Task ExactMetric_ViaRegistry()
    {
        var registry = MetricRegistry.CreateDefault();
        var metric = registry.GetRequired("exact");
        var result = await metric.EvaluateAsync(new MetricContext
        {
            Actual = "Paris",
            Expected = "paris",
            PassThreshold = 1.0
        });
        Assert.Equal(1.0, result.Score);
        Assert.True(result.IsPassed);
    }

    [Fact]
    public async Task SemanticMetric_ClarifiesTfIdfInDetails()
    {
        var result = await Eval.Direct()
            .Semantic("Paris is the capital of France", "Paris capital France")
            .WithThreshold(0.1)
            .EvaluateAsync();

        Assert.True(result.Score > 0);
        Assert.Equal("semantic", result.MetricName);
        Assert.Contains("TF-IDF", result.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JsonMetric_ValidAndInvalid()
    {
        var ok = await Eval.Direct().Json("""{"a":1}""").EvaluateAsync();
        ok.ShouldPass();
        Assert.Equal("json", ok.MetricName);

        var bad = await Eval.Direct().Json("{not-json").EvaluateAsync();
        Assert.False(bad.IsPassed);
        Assert.Contains("Invalid JSON", bad.Details);
    }

    [Fact]
    public async Task SchemaMetric_ValidatesRequiredProperties()
    {
        var schema = """
        {
          "type": "object",
          "required": ["name", "age"],
          "properties": {
            "name": { "type": "string" },
            "age": { "type": "integer", "minimum": 0 }
          }
        }
        """;

        var pass = await Eval.Direct()
            .Schema("""{"name":"Ada","age":36}""", schema)
            .EvaluateAsync();
        pass.ShouldPass();

        var fail = await Eval.Direct()
            .Schema("""{"name":"Ada"}""", schema)
            .EvaluateAsync();
        Assert.False(fail.IsPassed);
        Assert.Contains("required", fail.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RelevanceMetric_ScoresQuestionOverlap()
    {
        var result = await Eval.Direct()
            .Relevance(
                question: "What is the capital of France?",
                actual: "The capital of France is Paris",
                expected: "Paris")
            .WithThreshold(0.1)
            .EvaluateAsync();

        Assert.Equal("relevance", result.MetricName);
        Assert.True(result.Score > 0);
        result.ShouldPass();
    }

    [Fact]
    public async Task HeuristicGrounding_DetectsUnsupportedClaim()
    {
        var grounded = await Eval.Direct()
            .GroundedHeuristic(
                actual: "Paris is the capital of France.",
                reference: "Paris is the capital of France. It is in Europe.")
            .WithThreshold(0.5)
            .EvaluateAsync();

        grounded.ShouldPass();
        Assert.NotNull(grounded.GroundednessScore);
        Assert.True(grounded.HallucinationRate is null or <= 0.5);

        var hallucinated = await Eval.Direct()
            .GroundedHeuristic(
                actual: "The moon is made of cheese entirely.",
                reference: "Paris is the capital of France.")
            .WithThreshold(0.8)
            .EvaluateAsync();

        Assert.False(hallucinated.IsPassed);
        Assert.Contains("Unsupported", hallucinated.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomMetric_CanBeRegisteredWithoutForking()
    {
        var registry = MetricRegistry.CreateDefault();
        registry.Register(new AlwaysHalfMetric());
        var service = new AdvancedEvaluationService(new AiProviderFactory(), new HttpClient(), null, registry);

        var result = await service.EvaluateAsync(new EvaluationRequest
        {
            AiResponse = "x",
            GoldenOutput = "y",
            MatchingType = "always-half",
            PassThreshold = 0.4,
            EvaluationType = EvaluationType.DirectEvaluation
        });

        Assert.Equal(0.5, result.Score);
        Assert.True(result.IsPassed);
        Assert.Equal("always-half", result.MetricName);
    }

    [Fact]
    public void AddLLMEval_RegistersMetricRegistry_AndCustomMetric()
    {
        var services = new ServiceCollection();
        services.AddLLMEvalMetric<AlwaysHalfMetric>();
        services.AddLLMEval();
        using var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<MetricRegistry>();
        Assert.True(registry.TryGet("always-half", out _));
    }

    private sealed class AlwaysHalfMetric : IEvaluationMetric
    {
        public string Name => "always-half";
        public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new MetricResult
            {
                Score = 0.5,
                IsPassed = 0.5 >= context.PassThreshold,
                Details = "always 0.5"
            });
    }
}

public class TokenUsageParserTests
{
    [Fact]
    public void ParsesOpenAiUsage_AndEstimatesCost()
    {
        var json = """
        {
          "choices":[{"message":{"content":"0.9 ok"}}],
          "usage":{"prompt_tokens":100,"completion_tokens":50,"total_tokens":150}
        }
        """;
        var usage = TokenUsageParser.TryParse(json, new Dictionary<string, string>
        {
            ["InputCostPer1M"] = "1",
            ["OutputCostPer1M"] = "2"
        });

        Assert.NotNull(usage);
        Assert.Equal(100, usage!.PromptTokens);
        Assert.Equal(50, usage.CompletionTokens);
        Assert.Equal(150, usage.TotalTokens);
        Assert.NotNull(usage.EstimatedCostUsd);
        Assert.Equal(0.0002m, usage.EstimatedCostUsd); // 100/1e6*1 + 50/1e6*2
    }

    [Fact]
    public void ParsesGeminiUsageMetadata()
    {
        var json = """
        {
          "candidates":[{"content":{"parts":[{"text":"SUPPORTED"}]}}],
          "usageMetadata":{"promptTokenCount":10,"candidatesTokenCount":3,"totalTokenCount":13}
        }
        """;
        var usage = TokenUsageParser.TryParse(json);
        Assert.NotNull(usage);
        Assert.Equal(10, usage!.PromptTokens);
        Assert.Equal(3, usage.CompletionTokens);
        Assert.Equal(13, usage.TotalTokens);
    }

    [Fact]
    public void ReturnsNull_WhenNoUsage()
    {
        Assert.Null(TokenUsageParser.TryParse("""{"choices":[{"message":{"content":"hi"}}]}"""));
    }

    [Fact]
    public async Task Judge_AttachesUsage_WhenProviderReturnsIt()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            """{"choices":[{"message":{"content":"0.9 ok"}}],"usage":{"prompt_tokens":11,"completion_tokens":4,"total_tokens":15}}"""
        };
        var service = new AdvancedEvaluationService(factory);
        var result = await service.EvaluateAsync(new EvaluationRequest
        {
            Question = "Q",
            AiResponse = "A",
            GoldenOutput = "E",
            ProviderType = ProviderType.OpenAI,
            Endpoint = "https://api.openai.com/v1/chat/completions",
            PassThreshold = 0.8,
            EvaluationType = EvaluationType.LLMAsJudge
        });

        Assert.NotNull(result.Usage);
        Assert.Equal(15, result.Usage!.TotalTokens);
    }
}

public class CsvDatasetAndReportTests
{
    [Fact]
    public void ParseDataset_Csv()
    {
        var csv = """
        id,question,actual,expected,matchingType,threshold
        c1,Capital?,Paris,Paris,exact,1.0
        c2,Capital?,"Paris, France",Paris France,keyword,0.5
        """;
        var cases = EvaluationSuite.ParseDataset(csv, "cases.csv");
        Assert.Equal(2, cases.Count);
        Assert.Equal("c1", cases[0].Id);
        Assert.Equal("Paris, France", cases[1].Actual);
        Assert.Equal("keyword", cases[1].MatchingType);
    }

    [Fact]
    public async Task WriteReports_IncludesMarkdownAndCsv()
    {
        var cases = new[]
        {
            new SuiteCase { Id = "1", Actual = "Paris", Expected = "Paris", MatchingType = "exact", Threshold = 1.0 }
        };
        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));
        var result = await suite.RunAsync(cases);
        var dir = Path.Combine(Path.GetTempPath(), "llmeval-p2-" + Guid.NewGuid().ToString("N"));
        try
        {
            await suite.WriteReportsAsync(result, dir);
            Assert.True(File.Exists(Path.Combine(dir, "report.md")));
            Assert.True(File.Exists(Path.Combine(dir, "report.csv")));
            var md = await File.ReadAllTextAsync(Path.Combine(dir, "report.md"));
            Assert.Contains("Pass rate", md);
            Assert.Contains("PASS", md);
            var csv = await File.ReadAllTextAsync(Path.Combine(dir, "report.csv"));
            Assert.Contains("id,passed,score", csv);
            Assert.Contains("Paris", csv);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}

public class BaselineComparerTests
{
    [Fact]
    public void Compare_DetectsRegressionAndImprovement()
    {
        var baseline = new SuiteRunResult
        {
            PassRate = 1.0,
            Passed = 2,
            Total = 2,
            Cases = new[]
            {
                new SuiteCaseResult { Id = "a", Score = 1.0, Passed = true },
                new SuiteCaseResult { Id = "b", Score = 0.9, Passed = true }
            }
        };
        var current = new SuiteRunResult
        {
            PassRate = 0.5,
            Passed = 1,
            Total = 2,
            Cases = new[]
            {
                new SuiteCaseResult { Id = "a", Score = 0.0, Passed = false },
                new SuiteCaseResult { Id = "b", Score = 1.0, Passed = true }
            }
        };

        var diff = BaselineComparer.Compare(current, baseline);
        Assert.True(diff.HasRegressions);
        Assert.Contains("a", diff.NewFailures);
        Assert.Contains(diff.Improvements, i => i.Id == "b");
        Assert.Contains("REGRESS", diff.ToSummary());
    }

    [Fact]
    public async Task CompareToBaselineFile_RoundTrip()
    {
        var baseline = new SuiteRunResult
        {
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            PassRate = 1.0,
            Passed = 1,
            Total = 1,
            Cases = new[] { new SuiteCaseResult { Id = "x", Score = 1.0, Passed = true, Actual = "Paris", Expected = "Paris" } }
        };
        var dir = Path.Combine(Path.GetTempPath(), "llmeval-base-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "report.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(baseline));

            var current = new SuiteRunResult
            {
                PassRate = 1.0,
                Passed = 1,
                Total = 1,
                Cases = new[] { new SuiteCaseResult { Id = "x", Score = 1.0, Passed = true } }
            };
            var diff = await BaselineComparer.CompareToBaselineFileAsync(current, path);
            Assert.False(diff.HasRegressions);
            await BaselineComparer.WriteDiffReportAsync(diff, dir);
            Assert.True(File.Exists(Path.Combine(dir, "baseline-diff.md")));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}

public class GroundingHallucinationFieldsTests
{
    [Fact]
    public async Task Grounding_SetsHallucinationRateAndGroundednessScore()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            """{"response":"SUPPORTED","done":true}""",
            """{"response":"UNSUPPORTED","done":true}"""
        };
        var service = new AdvancedEvaluationService(factory);
        var result = await service.EvaluateAsync(new EvaluationRequest
        {
            Question = "Q",
            AiResponse = "Claim one. Claim two.",
            GoldenOutput = "Reference for claim one only.",
            ProviderType = ProviderType.Ollama,
            Endpoint = "http://localhost",
            PassThreshold = 0.5,
            EvaluationType = EvaluationType.GroundedAnswerCheck,
            Configuration = new Dictionary<string, string> { ["Model"] = "t" }
        });

        Assert.Equal(0.5, result.GroundednessScore);
        Assert.Equal(0.5, result.HallucinationRate);
        Assert.NotNull(result.UnsupportedStatements);
        Assert.Single(result.UnsupportedStatements!);
    }
}
