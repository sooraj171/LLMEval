using System.Net;
using System.Text;
using LLMEval.Integrations.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LLMEval.Tests;

public class Phase4ProviderTests
{
    [Fact]
    public void AiProviderFactory_CreatesClaudeGroqMistral()
    {
        var factory = new AiProviderFactory();
        using var http = new HttpClient();

        Assert.IsType<ClaudeProvider>(factory.CreateProvider(ProviderType.Claude, http));
        Assert.IsType<GroqProvider>(factory.CreateProvider(ProviderType.Groq, http));
        Assert.IsType<MistralProvider>(factory.CreateProvider(ProviderType.Mistral, http));
    }

    [Fact]
    public async Task ClaudeProvider_PostsMessagesApi()
    {
        var handler = new StubHttpHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("https://api.anthropic.com/v1/messages", req.RequestUri!.ToString());
            Assert.True(req.Headers.Contains("x-api-key"));
            Assert.True(req.Headers.Contains("anthropic-version"));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"content":[{"type":"text","text":"0.91 claude-ok"}],"usage":{"input_tokens":10,"output_tokens":5}}""",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        using var http = new HttpClient(handler);
        var provider = new ClaudeProvider(http);
        var json = await provider.GetResponseAsync(
            "",
            "score this",
            new Dictionary<string, string>
            {
                ["ApiKey"] = "test-key",
                ["Model"] = "claude-3-5-haiku-latest",
                ["Temperature"] = "0"
            });

        Assert.Contains("claude-ok", json);
        var usage = TokenUsageParser.TryParse(json);
        Assert.NotNull(usage);
        Assert.Equal(10, usage!.PromptTokens);
        Assert.Equal(5, usage.CompletionTokens);
    }

    [Fact]
    public async Task ClaudeProvider_MissingApiKey_Throws()
    {
        using var http = new HttpClient(new StubHttpHandler((_, _) => throw new Exception("no call")));
        var provider = new ClaudeProvider(http);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetResponseAsync("", "p", new Dictionary<string, string>()));
    }

    [Fact]
    public void LLMResponseParser_ParseClaudeEvaluationResponse()
    {
        var json = """{"content":[{"type":"text","text":"0.88 looks good"}]}""";
        var parsed = LLMResponseParser.ParseClaudeEvaluationResponse(json);
        Assert.Equal("0.88", parsed.ScoreString);
        Assert.Contains("looks good", parsed.Description);
    }

    [Fact]
    public async Task EvaluateAsync_ClaudeJudge_ParsesScore()
    {
        var handler = new StubHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"content":[{"type":"text","text":"0.95 reason"}],"usage":{"input_tokens":3,"output_tokens":2}}""",
                    Encoding.UTF8,
                    "application/json")
            }));

        using var http = new HttpClient(handler);
        var service = new AdvancedEvaluationService(new AiProviderFactory(), http);
        var result = await service.EvaluateAsync(new EvaluationRequest
        {
            Question = "Q",
            AiResponse = "A",
            GoldenOutput = "E",
            ProviderType = ProviderType.Claude,
            Configuration = new Dictionary<string, string>
            {
                ["ApiKey"] = "k",
                ["Model"] = "claude-3-5-haiku-latest"
            },
            PassThreshold = 0.8,
            EvaluationType = EvaluationType.LLMAsJudge
        });

        Assert.Equal(0.95, result.Score);
        Assert.True(result.IsPassed);
        Assert.Contains("reason", result.Details);
        Assert.NotNull(result.Usage);
    }

    [Fact]
    public async Task GroqProvider_UsesDefaultEndpoint()
    {
        var handler = new StubHttpHandler((req, _) =>
        {
            Assert.Equal("https://api.groq.com/openai/v1/chat/completions", req.RequestUri!.ToString());
            Assert.NotNull(req.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"role":"assistant","content":"0.7 ok"}}]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        using var http = new HttpClient(handler);
        var json = await new GroqProvider(http).GetResponseAsync(
            "",
            "p",
            new Dictionary<string, string> { ["ApiKey"] = "g", ["Model"] = "llama-3.3-70b-versatile" });
        Assert.Contains("0.7 ok", json);
    }

    [Fact]
    public async Task MistralProvider_UsesDefaultEndpoint()
    {
        var handler = new StubHttpHandler((req, _) =>
        {
            Assert.Equal("https://api.mistral.ai/v1/chat/completions", req.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"choices":[{"message":{"role":"assistant","content":"0.8 m"}}]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        using var http = new HttpClient(handler);
        var json = await new MistralProvider(http).GetResponseAsync(
            "",
            "p",
            new Dictionary<string, string> { ["ApiKey"] = "m" });
        Assert.Contains("0.8 m", json);
    }
}

public class Phase4ArchitectureTests
{
    [Fact]
    public void AbstractionsTypes_LiveInAbstractionsAssembly()
    {
        Assert.Equal("LLMEval.Abstractions", typeof(IEvaluationService).Assembly.GetName().Name);
        Assert.Equal("LLMEval.Abstractions", typeof(EvaluationRequest).Assembly.GetName().Name);
        Assert.Equal("LLMEval.Abstractions", typeof(IAiProvider).Assembly.GetName().Name);
        Assert.Equal("LLMEval.Abstractions", typeof(ProviderType).Assembly.GetName().Name);
    }

    [Fact]
    public void CoreTypes_LiveInCoreAssembly()
    {
        Assert.Equal("LLMEval.Core", typeof(AdvancedEvaluationService).Assembly.GetName().Name);
        Assert.Equal("LLMEval.Core", typeof(Eval).Assembly.GetName().Name);
        Assert.Equal("LLMEval.Core", typeof(ClaudeProvider).Assembly.GetName().Name);
    }

    [Fact]
    public void MetaPackage_ForwardsPublicTypes()
    {
        // Compiling against the meta project resolves types via TypeForwards / ProjectReference.
        Assert.NotNull(typeof(EvaluationRequest));
        Assert.NotNull(typeof(AdvancedEvaluationService));
        Assert.Contains(ProviderType.Claude.ToString(), Enum.GetNames<ProviderType>());
        Assert.Contains(ProviderType.Groq.ToString(), Enum.GetNames<ProviderType>());
        Assert.Contains(ProviderType.Mistral.ToString(), Enum.GetNames<ProviderType>());
    }

    [Fact]
    public void AddLLMEval_FromConfiguration_BindsSection()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LLMEval:DefaultProvider"] = "Claude",
                ["LLMEval:ApiKey"] = "cfg-key",
                ["LLMEval:Model"] = "claude-3-5-haiku-latest",
                ["LLMEval:DefaultPassThreshold"] = "0.75"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLLMEval(config);

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LLMEvalOptions>>().Value;
        Assert.Equal(ProviderType.Claude, options.DefaultProvider);
        Assert.Equal("cfg-key", options.ApiKey);
        Assert.Equal("claude-3-5-haiku-latest", options.Model);
        Assert.Equal(0.75, options.DefaultPassThreshold);
        Assert.NotNull(sp.GetRequiredService<IEvaluationService>());
    }
}

public class Phase4SemanticKernelTests
{
    [Fact]
    public async Task SemanticKernelChatProvider_WrapsResponseAsOpenAIJson()
    {
        var chat = new FakeChatCompletionService("0.9 from-sk");
        var provider = new SemanticKernelChatProvider(chat);
        var json = await provider.GetResponseAsync(
            "ignored",
            "prompt",
            new Dictionary<string, string> { ["Temperature"] = "0" });

        var parsed = LLMResponseParser.ParseOpenAIEvaluationResponse(json);
        Assert.Equal("0.9", parsed.ScoreString);
        Assert.Contains("from-sk", parsed.Description);
    }

    [Fact]
    public void SemanticKernelProviderFactory_ReturnsSkProvider()
    {
        var factory = new SemanticKernelProviderFactory(new FakeChatCompletionService("x"));
        using var http = new HttpClient();
        Assert.IsType<SemanticKernelChatProvider>(factory.CreateProvider(ProviderType.OpenAI, http));
    }

    private sealed class FakeChatCompletionService : IChatCompletionService
    {
        private readonly string _content;

        public FakeChatCompletionService(string content) => _content = content;

        public IReadOnlyDictionary<string, object?> Attributes { get; } =
            new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ChatMessageContent> list = new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, _content)
            };
            return Task.FromResult(list);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
