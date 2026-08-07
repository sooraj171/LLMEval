namespace LLMEval;

/// <summary>
/// Registry of DirectEvaluation metrics. Built-ins are registered by default;
/// call <see cref="Register"/> to add custom metrics without forking core.
/// </summary>
public sealed class MetricRegistry
{
    private readonly Dictionary<string, IEvaluationMetric> _metrics =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a registry with all built-in metrics.</summary>
    public static MetricRegistry CreateDefault()
    {
        var registry = new MetricRegistry();
        registry.RegisterBuiltIns();
        return registry;
    }

    /// <summary>Registers or replaces a metric by <see cref="IEvaluationMetric.Name"/>.</summary>
    public MetricRegistry Register(IEvaluationMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (string.IsNullOrWhiteSpace(metric.Name))
            throw new ArgumentException("Metric name is required.", nameof(metric));
        _metrics[metric.Name.Trim()] = metric;
        return this;
    }

    /// <summary>Tries to resolve a metric by MatchingType / name.</summary>
    public bool TryGet(string name, out IEvaluationMetric metric)
    {
        metric = null!;
        if (string.IsNullOrWhiteSpace(name)) return false;
        return _metrics.TryGetValue(name.Trim(), out metric!);
    }

    public IEvaluationMetric GetRequired(string name)
    {
        if (TryGet(name, out var metric)) return metric;
        throw new KeyNotFoundException(
            $"No evaluation metric registered for '{name}'. Known: {string.Join(", ", Names)}.");
    }

    public IReadOnlyCollection<string> Names => _metrics.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();

    public IEnumerable<IEvaluationMetric> All => _metrics.Values;

    private void RegisterBuiltIns()
    {
        Register(new ExactMatchMetric());
        Register(new KeywordMatchMetric());
        Register(new SemanticSimilarityMetric());
        Register(new JsonValidityMetric());
        Register(new JsonSchemaMetric());
        Register(new RelevanceMetric());
        Register(new HeuristicGroundingMetric());
    }
}
