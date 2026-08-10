using System.Globalization;
using System.Text.Json;

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

/// <summary>Parses usage blocks from OpenAI / Azure / Gemini / Ollama JSON when present.</summary>
public static class TokenUsageParser
{
    /// <summary>
    /// Tries to extract token usage. Optional config keys:
    /// InputCostPer1M / OutputCostPer1M (USD per 1M tokens) for EstimatedCostUsd.
    /// </summary>
    public static TokenUsage? TryParse(string? providerJson, IReadOnlyDictionary<string, string>? configuration = null)
    {
        if (string.IsNullOrWhiteSpace(providerJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(providerJson);
            var root = doc.RootElement;
            var usage = new TokenUsage();

            if (root.TryGetProperty("usage", out var usageEl))
            {
                // OpenAI / Azure
                usage.PromptTokens = GetInt(usageEl, "prompt_tokens") ?? GetInt(usageEl, "input_tokens");
                usage.CompletionTokens = GetInt(usageEl, "completion_tokens") ?? GetInt(usageEl, "output_tokens");
                usage.TotalTokens = GetInt(usageEl, "total_tokens");
            }
            else if (root.TryGetProperty("usageMetadata", out var geminiUsage))
            {
                usage.PromptTokens = GetInt(geminiUsage, "promptTokenCount");
                usage.CompletionTokens = GetInt(geminiUsage, "candidatesTokenCount");
                usage.TotalTokens = GetInt(geminiUsage, "totalTokenCount");
            }
            else if (root.TryGetProperty("prompt_eval_count", out _) || root.TryGetProperty("eval_count", out _))
            {
                // Ollama
                usage.PromptTokens = GetInt(root, "prompt_eval_count");
                usage.CompletionTokens = GetInt(root, "eval_count");
            }
            else
            {
                return null;
            }

            if (usage.TotalTokens == null && (usage.PromptTokens != null || usage.CompletionTokens != null))
                usage.TotalTokens = (usage.PromptTokens ?? 0) + (usage.CompletionTokens ?? 0);

            if (usage.PromptTokens == null && usage.CompletionTokens == null && usage.TotalTokens == null)
                return null;

            usage.EstimatedCostUsd = TryEstimateCost(usage, configuration);
            return usage;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? GetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)) return i;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var l)) return (int)l;
        return null;
    }

    private static decimal? TryEstimateCost(TokenUsage usage, IReadOnlyDictionary<string, string>? configuration)
    {
        if (configuration == null) return null;
        if (!TryGetDecimal(configuration, "InputCostPer1M", out var inPerM) &&
            !TryGetDecimal(configuration, "PromptCostPer1M", out inPerM))
            inPerM = 0;
        if (!TryGetDecimal(configuration, "OutputCostPer1M", out var outPerM) &&
            !TryGetDecimal(configuration, "CompletionCostPer1M", out outPerM))
            outPerM = 0;

        if (inPerM == 0 && outPerM == 0) return null;

        var prompt = usage.PromptTokens ?? 0;
        var completion = usage.CompletionTokens ?? 0;
        var cost = (prompt / 1_000_000m) * inPerM + (completion / 1_000_000m) * outPerM;
        return Math.Round(cost, 6);
    }

    private static bool TryGetDecimal(IReadOnlyDictionary<string, string> configuration, string key, out decimal value)
    {
        value = 0;
        if (!configuration.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return false;
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
