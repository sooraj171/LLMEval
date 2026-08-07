namespace LLMEval;

/// <summary>Best-effort token / cost usage when a provider response includes usage metadata.</summary>
public sealed class TokenUsage
{
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }

    /// <summary>Estimated USD cost when pricing is configured or a known default applies; otherwise null.</summary>
    public decimal? EstimatedCostUsd { get; set; }

    public static TokenUsage Combine(params TokenUsage?[] parts)
    {
        var result = new TokenUsage();
        foreach (var p in parts)
        {
            if (p == null) continue;
            result.PromptTokens = Add(result.PromptTokens, p.PromptTokens);
            result.CompletionTokens = Add(result.CompletionTokens, p.CompletionTokens);
            result.TotalTokens = Add(result.TotalTokens, p.TotalTokens);
            if (p.EstimatedCostUsd.HasValue)
                result.EstimatedCostUsd = (result.EstimatedCostUsd ?? 0) + p.EstimatedCostUsd.Value;
        }

        if (result.TotalTokens == null && (result.PromptTokens != null || result.CompletionTokens != null))
            result.TotalTokens = (result.PromptTokens ?? 0) + (result.CompletionTokens ?? 0);

        return result;
    }

    private static int? Add(int? a, int? b)
    {
        if (a == null && b == null) return null;
        return (a ?? 0) + (b ?? 0);
    }
}
