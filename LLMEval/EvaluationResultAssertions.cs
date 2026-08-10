using System.Globalization;
using System.Text;

namespace LLMEval;

/// <summary>Thrown when an evaluation assertion fails.</summary>
public class LLMEvalAssertionException : Exception
{
    public EvaluationResult? Result { get; }

    /// <summary>Suite aggregate when a suite-level assertion failed.</summary>
    public SuiteRunResult? SuiteResult { get; }

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

    public LLMEvalAssertionException(string message, SuiteRunResult suiteResult)
        : base(message)
    {
        SuiteResult = suiteResult ?? throw new ArgumentNullException(nameof(suiteResult));
    }
}

/// <summary>Fluent assertion helpers for <see cref="EvaluationResult"/> and <see cref="SuiteRunResult"/>.</summary>
public static class EvaluationResultAssertions
{
    /// <summary>Asserts <see cref="EvaluationResult.IsPassed"/> is true.</summary>
    /// <param name="result">Evaluation result under test.</param>
    /// <param name="because">Optional reason included in the failure message (CI / triage).</param>
    public static EvaluationResult ShouldPass(this EvaluationResult result, string? because = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsPassed)
        {
            throw new LLMEvalAssertionException(
                FormatFailure("Expected evaluation to pass.", result, because),
                result);
        }
        return result;
    }

    /// <summary>Asserts score is strictly greater than <paramref name="minimum"/>.</summary>
    /// <param name="result">Evaluation result under test.</param>
    /// <param name="minimum">Exclusive lower bound for <see cref="EvaluationResult.Score"/>.</param>
    /// <param name="because">Optional reason included in the failure message (CI / triage).</param>
    public static EvaluationResult ShouldScoreAbove(this EvaluationResult result, double minimum, string? because = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Score <= minimum)
        {
            throw new LLMEvalAssertionException(
                FormatFailure($"Expected score > {minimum}, but was {result.Score}.", result, because),
                result);
        }
        return result;
    }

    /// <summary>Asserts grounding risk is not High and there are no unsupported statements.</summary>
    /// <param name="result">Evaluation result under test.</param>
    /// <param name="because">Optional reason included in the failure message (CI / triage).</param>
    public static EvaluationResult ShouldBeGrounded(this EvaluationResult result, string? because = null)
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
                    result,
                    because),
                result);
        }
        return result;
    }

    /// <summary>
    /// Asserts suite pass rate meets or exceeds <paramref name="minimumPassRate"/> (0–1).
    /// Useful as a CI gate: <c>report.ShouldMeetPassRate(0.9)</c>.
    /// </summary>
    /// <param name="result">Suite run under test.</param>
    /// <param name="minimumPassRate">Minimum acceptable pass rate (0–1).</param>
    /// <param name="because">Optional reason included in the failure message.</param>
    public static SuiteRunResult ShouldMeetPassRate(
        this SuiteRunResult result,
        double minimumPassRate,
        string? because = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.MeetsPassRate(minimumPassRate))
        {
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"Expected pass rate >= {minimumPassRate:P1}, but was {result.PassRate:P1} ({result.Passed}/{result.Total} passed).");
            var failed = result.Cases.Where(c => !c.Passed).Take(10).ToList();
            if (failed.Count > 0)
            {
                sb.AppendLine();
                sb.Append("Failed cases:");
                foreach (var c in failed)
                {
                    sb.AppendLine();
                    sb.Append(CultureInfo.InvariantCulture,
                        $"  - {c.Id}: score={c.Score:0.###}, metric={c.MetricName ?? "n/a"}, details={Truncate(c.Details, 160)}");
                }
                if (result.Failed > failed.Count)
                    sb.AppendLine().Append(CultureInfo.InvariantCulture, $"  … and {result.Failed - failed.Count} more");
            }
            if (!string.IsNullOrWhiteSpace(because))
            {
                sb.AppendLine();
                sb.Append("Because: ").Append(because);
            }

            throw new LLMEvalAssertionException(sb.ToString(), result);
        }
        return result;
    }

    internal static string FormatFailure(string headline, EvaluationResult result, string? because = null)
    {
        var sb = new StringBuilder();
        sb.Append(headline);
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture,
            $"Score={result.Score}, Passed={result.IsPassed}, Metric={result.MetricName ?? "n/a"}, Risk={result.RiskLevel ?? "n/a"}");

        if (result.GroundednessScore.HasValue || result.HallucinationRate.HasValue)
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture,
                $"Groundedness={Fmt(result.GroundednessScore)}, HallucinationRate={Fmt(result.HallucinationRate)}");
        }

        if (result.Usage != null &&
            (result.Usage.TotalTokens != null || result.Usage.EstimatedCostUsd != null))
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture,
                $"Usage: tokens={result.Usage.TotalTokens?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");
            if (result.Usage.EstimatedCostUsd.HasValue)
                sb.Append(CultureInfo.InvariantCulture, $", estCostUsd={result.Usage.EstimatedCostUsd:0.######}");
        }

        var partial = result.PartiallySupportedStatements;
        if (partial is { Count: > 0 })
        {
            sb.AppendLine();
            sb.Append("PartiallySupported: ").Append(string.Join("; ", partial.Take(5)));
            if (partial.Count > 5)
                sb.Append(CultureInfo.InvariantCulture, $" … (+{partial.Count - 5} more)");
        }

        sb.AppendLine();
        sb.Append("Details=").Append(Truncate(result.Details, 400));

        if (!string.IsNullOrWhiteSpace(because))
        {
            sb.AppendLine();
            sb.Append("Because: ").Append(because);
        }

        return sb.ToString();
    }

    private static string Fmt(double? value) =>
        value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "n/a";

    private static string Truncate(string? s, int max)
    {
        s ??= string.Empty;
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
