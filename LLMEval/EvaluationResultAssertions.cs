namespace LLMEval;

/// <summary>Thrown when an evaluation assertion fails.</summary>
public class LLMEvalAssertionException : Exception
{
    public EvaluationResult? Result { get; }

    public LLMEvalAssertionException(string message, EvaluationResult? result = null)
        : base(message)
    {
        Result = result;
    }

    public LLMEvalAssertionException(string message, EvaluationResult? result, Exception inner)
        : base(message, inner)
    {
        Result = result;
    }
}

/// <summary>Fluent assertion helpers for <see cref="EvaluationResult"/>.</summary>
public static class EvaluationResultAssertions
{
    /// <summary>Asserts <see cref="EvaluationResult.IsPassed"/> is true.</summary>
    public static EvaluationResult ShouldPass(this EvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsPassed)
        {
            throw new LLMEvalAssertionException(
                FormatFailure("Expected evaluation to pass.", result),
                result);
        }
        return result;
    }

    /// <summary>Asserts score is strictly greater than <paramref name="minimum"/>.</summary>
    public static EvaluationResult ShouldScoreAbove(this EvaluationResult result, double minimum)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Score <= minimum)
        {
            throw new LLMEvalAssertionException(
                FormatFailure($"Expected score > {minimum}, but was {result.Score}.", result),
                result);
        }
        return result;
    }

    /// <summary>Asserts grounding risk is not High and there are no unsupported statements.</summary>
    public static EvaluationResult ShouldBeGrounded(this EvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var unsupported = result.UnsupportedStatements ?? Array.Empty<string>();
        var risk = result.RiskLevel ?? string.Empty;
        if (string.Equals(risk, "High", StringComparison.OrdinalIgnoreCase) || unsupported.Count > 0)
        {
            var unsupportedText = unsupported.Count == 0
                ? "(none)"
                : string.Join("; ", unsupported);
            throw new LLMEvalAssertionException(
                FormatFailure(
                    $"Expected grounded response (no High risk, no unsupported statements). Risk={risk}. Unsupported: {unsupportedText}",
                    result),
                result);
        }
        return result;
    }

    private static string FormatFailure(string headline, EvaluationResult result)
    {
        return $"{headline} Score={result.Score}, Passed={result.IsPassed}, Risk={result.RiskLevel ?? "n/a"}, Details={result.Details}";
    }
}
