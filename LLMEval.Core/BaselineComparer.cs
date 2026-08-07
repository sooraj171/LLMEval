using System.Text;
using System.Text.Json;

namespace LLMEval;

/// <summary>Per-case score / pass delta versus a baseline suite run.</summary>
public sealed class CaseScoreDelta
{
    public string Id { get; init; } = string.Empty;
    public double BaselineScore { get; init; }
    public double CurrentScore { get; init; }
    public double ScoreDelta => CurrentScore - BaselineScore;
    public bool BaselinePassed { get; init; }
    public bool CurrentPassed { get; init; }
}

/// <summary>Result of comparing a suite run to a golden baseline report.</summary>
public sealed class BaselineDiff
{
    public double BaselinePassRate { get; init; }
    public double CurrentPassRate { get; init; }
    public double PassRateDelta => CurrentPassRate - BaselinePassRate;

    public IReadOnlyList<CaseScoreDelta> Regressions { get; init; } = Array.Empty<CaseScoreDelta>();
    public IReadOnlyList<CaseScoreDelta> Improvements { get; init; } = Array.Empty<CaseScoreDelta>();
    public IReadOnlyList<string> NewFailures { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FixedCases { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingFromCurrent { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NewCases { get; init; } = Array.Empty<string>();

    /// <summary>True when any previously-passing case now fails, or score dropped beyond tolerance.</summary>
    public bool HasRegressions => Regressions.Count > 0 || NewFailures.Count > 0;

    /// <summary>Human-readable summary for CI logs.</summary>
    public string ToSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pass rate: {BaselinePassRate:P1} → {CurrentPassRate:P1} (Δ {PassRateDelta:+0.0%;-0.0%;0%})");
        sb.AppendLine($"Regressions: {Regressions.Count}, New failures: {NewFailures.Count}, Improvements: {Improvements.Count}, Fixed: {FixedCases.Count}");
        foreach (var r in Regressions.Take(10))
            sb.AppendLine($"  REGRESS {r.Id}: score {r.BaselineScore:0.###} → {r.CurrentScore:0.###} (Δ {r.ScoreDelta:+0.###;-0.###;0})");
        foreach (var id in NewFailures.Take(10))
            sb.AppendLine($"  NEW FAIL {id}");
        return sb.ToString().TrimEnd();
    }
}

/// <summary>Compares a suite run against a previous baseline <c>report.json</c> (golden dataset regression).</summary>
public static class BaselineComparer
{
    /// <summary>
    /// Compares current results to baseline. A regression is a pass→fail, or a score drop greater than <paramref name="scoreTolerance"/>.
    /// </summary>
    public static BaselineDiff Compare(
        SuiteRunResult current,
        SuiteRunResult baseline,
        double scoreTolerance = 0.0)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);

        var baselineById = baseline.Cases.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        var currentById = current.Cases.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

        var regressions = new List<CaseScoreDelta>();
        var improvements = new List<CaseScoreDelta>();
        var newFailures = new List<string>();
        var fixedCases = new List<string>();

        foreach (var cur in current.Cases)
        {
            if (!baselineById.TryGetValue(cur.Id, out var baseCase))
                continue;

            var delta = new CaseScoreDelta
            {
                Id = cur.Id,
                BaselineScore = baseCase.Score,
                CurrentScore = cur.Score,
                BaselinePassed = baseCase.Passed,
                CurrentPassed = cur.Passed
            };

            if (baseCase.Passed && !cur.Passed)
            {
                newFailures.Add(cur.Id);
                regressions.Add(delta);
            }
            else if (!baseCase.Passed && cur.Passed)
            {
                fixedCases.Add(cur.Id);
                improvements.Add(delta);
            }
            else if (cur.Score + scoreTolerance < baseCase.Score)
            {
                regressions.Add(delta);
            }
            else if (cur.Score > baseCase.Score + scoreTolerance)
            {
                improvements.Add(delta);
            }
        }

        return new BaselineDiff
        {
            BaselinePassRate = baseline.PassRate,
            CurrentPassRate = current.PassRate,
            Regressions = regressions,
            Improvements = improvements,
            NewFailures = newFailures,
            FixedCases = fixedCases,
            MissingFromCurrent = baselineById.Keys.Where(id => !currentById.ContainsKey(id)).ToArray(),
            NewCases = currentById.Keys.Where(id => !baselineById.ContainsKey(id)).ToArray()
        };
    }

    /// <summary>Loads baseline JSON from disk and compares.</summary>
    public static async Task<BaselineDiff> CompareToBaselineFileAsync(
        SuiteRunResult current,
        string baselineReportJsonPath,
        double scoreTolerance = 0.0,
        CancellationToken cancellationToken = default)
    {
        var baseline = await EvaluationSuite.LoadBaselineAsync(baselineReportJsonPath, cancellationToken).ConfigureAwait(false);
        return Compare(current, baseline, scoreTolerance);
    }

    /// <summary>Writes a markdown baseline-diff summary next to other reports.</summary>
    public static async Task WriteDiffReportAsync(
        BaselineDiff diff,
        string outputDirectory,
        string fileName = "baseline-diff.md",
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, fileName);
        var sb = new StringBuilder();
        sb.AppendLine("# Baseline comparison");
        sb.AppendLine();
        sb.AppendLine(diff.ToSummary());
        sb.AppendLine();
        sb.AppendLine($"Has regressions: **{diff.HasRegressions}**");
        await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken).ConfigureAwait(false);
    }
}
