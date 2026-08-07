namespace LLMEval;

/// <summary>Input for <see cref="IEvaluationService.EvaluateAsync"/>.</summary>
public class EvaluationRequest
{
    public string Question { get; set; } = string.Empty;
    public string AiResponse { get; set; } = string.Empty;
    public string GoldenOutput { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public Dictionary<string, string> Configuration { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// DirectEvaluation metric name: exact, keyword, semantic (TF-IDF), json, schema, relevance, grounded-heuristic,
    /// or any custom name registered on the metric registry.
    /// </summary>
    public string MatchingType { get; set; } = string.Empty;

    public double PassThreshold { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public EvaluationType EvaluationType { get; set; }
    public bool IsReferenceDoc { get; set; }

    /// <summary>Optional: one or more reference documents for grounding. If null or empty, <see cref="GoldenOutput"/> is used as the single reference.</summary>
    public IReadOnlyList<string>? ReferenceDocuments { get; set; }

    /// <summary>Optional JSON Schema for MatchingType = schema (falls back to <see cref="GoldenOutput"/>).</summary>
    public string? Schema { get; set; }
}
