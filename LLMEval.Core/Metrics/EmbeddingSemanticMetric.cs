using System.Text.Json;

namespace LLMEval;

/// <summary>
/// Cosine similarity of embedding vectors (<c>embedding-semantic</c> and <c>semantic-embedding</c>).
/// Default backend is OpenAI / Azure OpenAI embeddings; inject <see cref="IEmbeddingProvider"/> to use another model.
/// TF-IDF remains the zero-dependency default via MatchingType = semantic.
/// A missing provider or API key throws <see cref="LLMEvalConfigurationException"/> instead of scoring zero.
/// </summary>
public sealed class EmbeddingSemanticMetric : IEvaluationMetric
{
    /// <summary>Canonical <see cref="IEvaluationMetric.Name"/> and MatchingType.</summary>
    public const string CanonicalName = "embedding-semantic";

    /// <summary>MatchingType alias registered to this same metric. Not the bare name "embedding".</summary>
    public const string AliasName = "semantic-embedding";

    /// <summary>Message thrown when neither an <see cref="IEmbeddingProvider"/> nor an API key is configured.</summary>
    public const string MissingProviderMessage =
        "SemanticEmbedding requires an IEmbeddingProvider. Configure via WithProvider(...) and WithApiKey(...), or register one with services.AddSingleton<IEmbeddingProvider, T>().";

    private readonly IEmbeddingProvider? _provider;
    private readonly HttpClient? _httpClient;
    private HttpClient? _ownedClient;

    public EmbeddingSemanticMetric()
        : this(provider: null, httpClient: null)
    {
    }

    public EmbeddingSemanticMetric(IEmbeddingProvider provider)
        : this(provider, httpClient: null)
    {
    }

    public EmbeddingSemanticMetric(HttpClient httpClient)
        : this(provider: null, httpClient)
    {
    }

    public EmbeddingSemanticMetric(IEmbeddingProvider? provider, HttpClient? httpClient)
    {
        _provider = provider;
        _httpClient = httpClient;
    }

    public string Name => CanonicalName;

    /// <summary>True for <see cref="CanonicalName"/> and <see cref="AliasName"/> only.</summary>
    public static bool IsEmbeddingMetricName(string name)
        => string.Equals(name, CanonicalName, StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, AliasName, StringComparison.OrdinalIgnoreCase);

    public async Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = _provider ?? CreateOpenAiProvider(context);
            var expected = context.Expected ?? string.Empty;
            var actual = context.Actual ?? string.Empty;
            var vectors = await provider.EmbedAsync(new[] { expected, actual }, cancellationToken).ConfigureAwait(false);
            if (vectors.Count < 2)
            {
                return Fail("Embeddings provider returned fewer than 2 vectors.");
            }

            var score = VectorMath.CosineSimilarity(vectors[0], vectors[1]);
            score = Math.Clamp(score, 0, 1);
            return new MetricResult
            {
                Score = score,
                IsPassed = score >= context.PassThreshold,
                Details = $"Embedding cosine similarity: {score:0.###}."
            };
        }
        catch (Exception ex) when (ex is ArgumentException or HttpRequestException or InvalidOperationException or JsonException)
        {
            return Fail(ex.Message);
        }
    }

    private IEmbeddingProvider CreateOpenAiProvider(MetricContext context)
    {
        var config = context.Configuration ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!HasApiKey(config))
            throw new LLMEvalConfigurationException(MissingProviderMessage);

        var http = _httpClient ?? (_ownedClient ??= new HttpClient());
        return new OpenAIEmbeddingProvider(http, config, context.Endpoint, context.ProviderType);
    }

    private static bool HasApiKey(IReadOnlyDictionary<string, string> config)
    {
        foreach (var kv in config)
        {
            if (string.Equals(kv.Key, "ApiKey", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kv.Value))
                return true;
        }
        return false;
    }

    private static MetricResult Fail(string details) => new()
    {
        Score = 0,
        IsPassed = false,
        Details = details
    };
}
