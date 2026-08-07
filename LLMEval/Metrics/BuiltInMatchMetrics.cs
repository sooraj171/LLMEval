namespace LLMEval;

/// <summary>Case-insensitive exact string match (MatchingType = exact).</summary>
public sealed class ExactMatchMetric : IEvaluationMetric
{
    public string Name => "exact";

    public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        var score = string.Equals(
            context.Actual?.Trim(),
            context.Expected?.Trim(),
            StringComparison.OrdinalIgnoreCase)
            ? 1.0
            : 0.0;

        return Task.FromResult(new MetricResult
        {
            Score = score,
            IsPassed = score >= context.PassThreshold,
            Details = score >= 1.0 ? "Exact match." : "Not an exact match."
        });
    }
}

/// <summary>Keyword overlap of expected tokens in actual (MatchingType = keyword).</summary>
public sealed class KeywordMatchMetric : IEvaluationMetric
{
    public string Name => "keyword";

    public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        var responseKeywords = Split(context.Actual);
        var goldenKeywords = Split(context.Expected);

        double score;
        string details;
        if (goldenKeywords.Count == 0)
        {
            score = 1.0;
            details = "No expected keywords; treated as pass.";
        }
        else
        {
            var matched = goldenKeywords.Count(k => responseKeywords.Contains(k));
            score = (double)matched / goldenKeywords.Count;
            details = $"Keyword overlap: {matched}/{goldenKeywords.Count}.";
        }

        return Task.FromResult(new MetricResult
        {
            Score = score,
            IsPassed = score >= context.PassThreshold,
            Details = details
        });
    }

    private static HashSet<string> Split(string? text) =>
        (text ?? string.Empty)
            .ToLowerInvariant()
            .Split(new[] { ' ', '-', ',', '.', ';', ':', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
}

/// <summary>
/// TF-IDF cosine similarity between actual and expected (MatchingType = semantic).
/// This is lexical/vector similarity, not an embedding model — see package docs.
/// </summary>
public sealed class SemanticSimilarityMetric : IEvaluationMetric
{
    private readonly TfidfSimilarity _tfidf = new();

    public string Name => "semantic";

    public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        var (score, details) = _tfidf.Calculate(context.Expected ?? string.Empty, context.Actual ?? string.Empty);
        return Task.FromResult(new MetricResult
        {
            Score = score,
            IsPassed = score >= context.PassThreshold,
            Details = string.IsNullOrEmpty(details)
                ? "TF-IDF semantic similarity."
                : $"TF-IDF semantic similarity. {details}"
        });
    }
}
