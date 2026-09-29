namespace LLMEval;

/// <summary>Context passed to a plugin metric during DirectEvaluation (and custom metric runs).</summary>
public sealed class MetricContext
{
    public string Question { get; init; } = string.Empty;
    public string Actual { get; init; } = string.Empty;
    public string Expected { get; init; } = string.Empty;

    /// <summary>Optional JSON Schema text when validating structured output (MatchingType = schema).</summary>
    public string? Schema { get; init; }

    /// <summary>Pass threshold from the request (0–1).</summary>
    public double PassThreshold { get; init; } = 0.8;

    /// <summary>Optional provider configuration (pricing keys, metric params, etc.).</summary>
    public IReadOnlyDictionary<string, string> Configuration { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional HTTP endpoint (Azure resource URL or embeddings URL) from the evaluation request.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Request provider type (used by embeddings to distinguish OpenAI vs Azure OpenAI).</summary>
    public ProviderType ProviderType { get; init; }
}

/// <summary>Result produced by an <see cref="IEvaluationMetric"/>.</summary>
public sealed class MetricResult
{
    public double Score { get; init; }
    public bool IsPassed { get; init; }
    public string Details { get; init; } = string.Empty;
}

/// <summary>
/// Pluggable DirectEvaluation metric. Register custom metrics on the metric registry
/// without forking the core evaluation service.
/// </summary>
public interface IEvaluationMetric
{
    /// <summary>Stable name used as MatchingType (e.g. exact, semantic, json, schema, relevance).</summary>
    string Name { get; }

    Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default);
}
