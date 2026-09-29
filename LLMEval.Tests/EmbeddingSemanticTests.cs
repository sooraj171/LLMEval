using System.Net;
using System.Text;
using LLMEval;
using Microsoft.Extensions.DependencyInjection;

namespace LLMEval.Tests;

public class EmbeddingSemanticTests
{
    private const string ParaphraseActual = "A feline rested on the rug";
    private const string ParaphraseExpected = "The cat sat on the mat";

    [Fact]
    public void DefaultRegistry_IncludesEmbeddingSemantic()
    {
        var registry = MetricRegistry.CreateDefault();
        Assert.True(registry.TryGet("embedding-semantic", out var metric));
        Assert.True(registry.TryGet("semantic-embedding", out var alias));
        Assert.Same(metric, alias);
        Assert.IsType<EmbeddingSemanticMetric>(metric);
        Assert.False(registry.TryGet("embedding", out _));
    }

    [Fact]
    public async Task TfIdf_ScoresParaphraseLow_EmbeddingsScoreHigh()
    {
        var tfidf = await Eval.Direct()
            .Semantic(ParaphraseActual, ParaphraseExpected)
            .WithThreshold(0)
            .EvaluateAsync();

        Assert.Equal("semantic", tfidf.MetricName);
        Assert.Contains("TF-IDF", tfidf.Details, StringComparison.OrdinalIgnoreCase);
        Assert.True(tfidf.Score < 0.35, $"TF-IDF should stay low on paraphrases, was {tfidf.Score}");

        var similar = new float[] { 0.9f, 0.1f, 0f };
        var metric = new EmbeddingSemanticMetric(new StubEmbeddingProvider(_ => new[] { similar, similar }));
        var embed = await metric.EvaluateAsync(new MetricContext
        {
            Actual = ParaphraseActual,
            Expected = ParaphraseExpected,
            PassThreshold = 0.75
        });

        Assert.True(embed.IsPassed);
        Assert.True(embed.Score > 0.99);
        Assert.True(embed.Score > tfidf.Score);
        Assert.Contains("Embedding cosine", embed.Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TF-IDF semantic", embed.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmbeddingMetric_OrthogonalVectors_ScoreNearZero()
    {
        var metric = new EmbeddingSemanticMetric(new StubEmbeddingProvider(_ => new[]
        {
            new float[] { 1f, 0f },
            new float[] { 0f, 1f }
        }));
        var result = await metric.EvaluateAsync(new MetricContext
        {
            Actual = "a",
            Expected = "b",
            PassThreshold = 0.5
        });
        Assert.False(result.IsPassed);
        Assert.True(result.Score < 0.01);
    }

    [Fact]
    public async Task Semantic_TfIdf_DoesNotThrowWhenUnconfigured()
    {
        var result = await Eval.Direct()
            .Semantic("hello", "hello")
            .WithThreshold(0)
            .EvaluateAsync();

        Assert.Equal("semantic", result.MetricName);
        Assert.Contains("TF-IDF", result.Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SemanticEmbedding_WithoutProviderOrApiKey_Throws()
    {
        var ex = await Assert.ThrowsAsync<LLMEvalConfigurationException>(() =>
            Eval.Direct()
                .SemanticEmbedding("hello", "hello")
                .WithProvider(EmbeddingProviderType.OpenAI)
                .WithThreshold(0.85)
                .EvaluateAsync());

        Assert.Contains("WithProvider", ex.Message, StringComparison.Ordinal);
        Assert.Contains("WithApiKey", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IEmbeddingProvider", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmbeddingSemantic_WithoutProviderOrApiKey_Throws()
    {
        var ex = await Assert.ThrowsAsync<LLMEvalConfigurationException>(() =>
            Eval.Direct()
                .EmbeddingSemantic("hello", "hello")
                .WithThreshold(0.8)
                .EvaluateAsync());

        Assert.Equal(EmbeddingSemanticMetric.MissingProviderMessage, ex.Message);
    }

    [Fact]
    public async Task SemanticEmbedding_ViaService_UsesRegisteredProvider()
    {
        var similar = new float[] { 1f, 0f };
        var registry = MetricRegistry.CreateDefault();
        registry.Register(new EmbeddingSemanticMetric(new StubEmbeddingProvider(_ => new[] { similar, similar })));

        var service = new AdvancedEvaluationService(new AiProviderFactory(), new HttpClient(), null, registry);
        var result = await service.EvaluateAsync(new EvaluationRequest
        {
            AiResponse = ParaphraseActual,
            GoldenOutput = ParaphraseExpected,
            EvaluationType = EvaluationType.DirectEvaluation,
            MatchingType = "semantic-embedding",
            PassThreshold = 0.85
        });

        result.ShouldPass();
        Assert.Equal("semantic-embedding", result.MetricName);
    }

    [Fact]
    public async Task AddLLMEval_UsesRegisteredEmbeddingProvider()
    {
        var similar = new float[] { 0.2f, 0.8f };
        var services = new ServiceCollection();
        services.AddSingleton<IEmbeddingProvider>(new StubEmbeddingProvider(_ => new[] { similar, similar }));
        services.AddLLMEval();

        using var provider = services.BuildServiceProvider();
        var eval = provider.GetRequiredService<IEvaluationService>();
        var result = await eval.EvaluateAsync(new EvaluationRequest
        {
            AiResponse = "hello",
            GoldenOutput = "hello",
            EvaluationType = EvaluationType.DirectEvaluation,
            MatchingType = EmbeddingSemanticMetric.AliasName,
            PassThreshold = 0.85
        });

        result.ShouldPass();
        Assert.Equal("semantic-embedding", result.MetricName);
    }

    [Fact]
    public async Task EmbeddingHttpFailure_ReturnsFailedResult()
    {
        var handler = new StubHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("unauthorized", Encoding.UTF8, "application/json")
            }));
        using var http = new HttpClient(handler);
        var metric = new EmbeddingSemanticMetric(http);
        var result = await metric.EvaluateAsync(new MetricContext
        {
            Actual = "a",
            Expected = "b",
            PassThreshold = 0.5,
            Configuration = new Dictionary<string, string> { ["ApiKey"] = "sk-test" }
        });

        Assert.False(result.IsPassed);
        Assert.Contains("401", result.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmbeddingMetric_ViaRegistryAndService()
    {
        var registry = MetricRegistry.CreateDefault();
        registry.Register(new EmbeddingSemanticMetric(new StubEmbeddingProvider(_ => new[]
        {
            new float[] { 1f, 0f, 0f },
            new float[] { 1f, 0f, 0f }
        })));

        var service = new AdvancedEvaluationService(new AiProviderFactory(), new HttpClient(), null, registry);
        var result = await service.EvaluateAsync(new EvaluationRequest
        {
            AiResponse = ParaphraseActual,
            GoldenOutput = ParaphraseExpected,
            EvaluationType = EvaluationType.DirectEvaluation,
            MatchingType = "embedding-semantic",
            PassThreshold = 0.8
        });

        result.ShouldPass();
        Assert.Equal("embedding-semantic", result.MetricName);
    }

    [Fact]
    public async Task OpenAIEmbeddingProvider_PostsOpenAiUrl_AndParsesVectors()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHttpHandler(async (req, _) =>
        {
            seen = req;
            var body = await req.Content!.ReadAsStringAsync();
            Assert.Contains("text-embedding-3-small", body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":[{"index":1,"embedding":[0,1]},{"index":0,"embedding":[1,0]}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var http = new HttpClient(handler);
        var provider = new OpenAIEmbeddingProvider(
            http,
            new Dictionary<string, string> { ["ApiKey"] = "sk-test", ["Model"] = "text-embedding-3-small" });

        var vectors = await provider.EmbedAsync(new[] { "expected", "actual" });
        Assert.Equal("https://api.openai.com/v1/embeddings", seen!.RequestUri!.ToString());
        Assert.Equal("sk-test", AuthenticationHeaderValueBearer(seen));
        Assert.Equal(2, vectors.Count);
        Assert.Equal(1f, vectors[0][0]);
        Assert.Equal(1f, vectors[1][1]);
    }

    [Fact]
    public async Task OpenAIEmbeddingProvider_Azure_UsesApiKeyHeaderAndDeploymentUrl()
    {
        HttpRequestMessage? seen = null;
        var handler = new StubHttpHandler((req, _) =>
        {
            seen = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":[{"index":0,"embedding":[1]},{"index":1,"embedding":[1]}]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        using var http = new HttpClient(handler);
        var provider = new OpenAIEmbeddingProvider(
            http,
            new Dictionary<string, string>
            {
                ["ApiKey"] = "azure-key",
                ["EmbeddingModel"] = "text-embed-dep"
            },
            endpoint: "https://myres.openai.azure.com",
            providerType: ProviderType.AzureOpenAI);

        await provider.EmbedAsync(new[] { "a", "b" });
        Assert.NotNull(seen);
        Assert.Equal(
            "https://myres.openai.azure.com/openai/deployments/text-embed-dep/embeddings?api-version=2024-02-01",
            seen!.RequestUri!.ToString());
        Assert.True(seen.Headers.Contains("api-key"));
    }

    [Fact]
    public void BuildEmbeddingsUrl_Helpers()
    {
        Assert.Equal(
            "https://api.openai.com/v1/embeddings",
            OpenAIEmbeddingProvider.BuildOpenAiEmbeddingsUrl(null));
        Assert.Equal(
            "https://myres.openai.azure.com/openai/deployments/dep/embeddings?api-version=2024-02-01",
            OpenAIEmbeddingProvider.BuildAzureEmbeddingsUrl("https://myres.openai.azure.com", "dep", "2024-02-01"));
    }

    [Fact]
    public void VectorMath_IdenticalAndZero()
    {
        Assert.Equal(1.0, VectorMath.CosineSimilarity(new[] { 1f, 2f }, new[] { 1f, 2f }), 5);
        Assert.Equal(0.0, VectorMath.CosineSimilarity(new[] { 1f, 0f }, new[] { 0f, 1f }), 5);
        Assert.Equal(0.0, VectorMath.CosineSimilarity(Array.Empty<float>(), new[] { 1f }));
    }

    private static string AuthenticationHeaderValueBearer(HttpRequestMessage req)
        => req.Headers.Authorization?.Parameter ?? "";

    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        private readonly Func<IReadOnlyList<string>, IReadOnlyList<float[]>> _fn;

        public StubEmbeddingProvider(Func<IReadOnlyList<string>, IReadOnlyList<float[]>> fn) => _fn = fn;

        public Task<IReadOnlyList<float[]>> EmbedAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_fn(texts));
    }
}
