using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMEval;

/// <summary>A single case in an evaluation suite dataset.</summary>
public class SuiteCase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("actual")]
    public string Actual { get; set; } = string.Empty;

    [JsonPropertyName("expected")]
    public string Expected { get; set; } = string.Empty;

    [JsonPropertyName("evaluationType")]
    public string EvaluationType { get; set; } = "DirectEvaluation";

    [JsonPropertyName("matchingType")]
    public string MatchingType { get; set; } = "exact";

    [JsonPropertyName("threshold")]
    public double? Threshold { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("isReferenceDoc")]
    public bool IsReferenceDoc { get; set; }

    [JsonPropertyName("referenceDocuments")]
    public List<string>? ReferenceDocuments { get; set; }

    /// <summary>Optional JSON Schema for MatchingType = schema.</summary>
    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    /// <summary>
    /// Optional tags for filtering cases in CI (e.g. smoke, nightly).
    /// JSON/JSONL: string array. CSV: semicolon- or pipe-separated in a <c>tags</c> column.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }
}

/// <summary>Result for one suite case.</summary>
public class SuiteCaseResult
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public double Score { get; set; }
    public bool Passed { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? RiskLevel { get; set; }
    public string? MetricName { get; set; }
    public double? GroundednessScore { get; set; }
    public double? HallucinationRate { get; set; }
    public TokenUsage? Usage { get; set; }
}

/// <summary>Aggregate suite run result.</summary>
public class SuiteRunResult
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public double PassRate { get; set; }
    public IReadOnlyList<SuiteCaseResult> Cases { get; set; } = Array.Empty<SuiteCaseResult>();

    /// <summary>Aggregated token usage across cases when available.</summary>
    public TokenUsage? TotalUsage { get; set; }

    /// <summary>Returns true when pass rate meets or exceeds <paramref name="minimumPassRate"/> (0–1).</summary>
    public bool MeetsPassRate(double minimumPassRate) => PassRate >= minimumPassRate;
}

/// <summary>Helpers for filtering suite datasets by tag.</summary>
public static class SuiteCaseFiltering
{
    /// <summary>
    /// Filters cases that have the given tags.
    /// When <paramref name="requireAll"/> is false (default), a case matches if it has any listed tag.
    /// When true, the case must include every listed tag.
    /// Cases with no tags never match a non-empty tag filter.
    /// </summary>
    public static IReadOnlyList<SuiteCase> FilterByTags(
        this IEnumerable<SuiteCase> cases,
        IEnumerable<string> tags,
        bool requireAll = false)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(tags);
        var wanted = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (wanted.Count == 0)
            return cases.ToList();

        return cases.Where(c =>
        {
            var caseTags = c.Tags?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToList() ?? new List<string>();
            if (caseTags.Count == 0) return false;
            if (requireAll)
                return wanted.All(w => caseTags.Any(ct => string.Equals(ct, w, StringComparison.OrdinalIgnoreCase)));
            return wanted.Any(w => caseTags.Any(ct => string.Equals(ct, w, StringComparison.OrdinalIgnoreCase)));
        }).ToList();
    }

    /// <inheritdoc cref="FilterByTags(IEnumerable{SuiteCase}, IEnumerable{string}, bool)"/>
    public static IReadOnlyList<SuiteCase> FilterByTags(
        this IEnumerable<SuiteCase> cases,
        bool requireAll,
        params string[] tags) =>
        cases.FilterByTags(tags, requireAll);

    /// <inheritdoc cref="FilterByTags(IEnumerable{SuiteCase}, IEnumerable{string}, bool)"/>
    public static IReadOnlyList<SuiteCase> FilterByTags(
        this IEnumerable<SuiteCase> cases,
        params string[] tags) =>
        cases.FilterByTags(tags, requireAll: false);
}

/// <summary>Loads JSON/JSONL/CSV datasets and runs batch evaluations with optional reports.</summary>
public class EvaluationSuite
{
    private readonly IEvaluationService _evaluationService;
    private readonly LLMEvalOptions _options;

    public EvaluationSuite(IEvaluationService evaluationService, LLMEvalOptions? options = null)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _options = options ?? new LLMEvalOptions();
    }

    public static async Task<IReadOnlyList<SuiteCase>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return ParseDataset(text, path);
    }

    public static IReadOnlyList<SuiteCase> ParseDataset(string text, string? pathHint = null)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return Array.Empty<SuiteCase>();

        var isCsv = pathHint?.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) == true
                    || LooksLikeCsv(trimmed, pathHint);

        if (isCsv)
            return CsvDatasetParser.Parse(trimmed);

        var isJsonl = (pathHint?.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) == true)
                      || (!trimmed.StartsWith('[') && !trimmed.StartsWith('{'));

        if (!isJsonl && trimmed.StartsWith('['))
        {
            var list = JsonSerializer.Deserialize<List<SuiteCase>>(trimmed, JsonOptions())
                       ?? new List<SuiteCase>();
            return list;
        }

        if (!isJsonl && trimmed.StartsWith('{'))
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.TryGetProperty("cases", out var casesEl))
            {
                var list = JsonSerializer.Deserialize<List<SuiteCase>>(casesEl.GetRawText(), JsonOptions())
                           ?? new List<SuiteCase>();
                return list;
            }

            var single = JsonSerializer.Deserialize<SuiteCase>(trimmed, JsonOptions());
            return single == null ? Array.Empty<SuiteCase>() : new[] { single };
        }

        // JSONL
        var results = new List<SuiteCase>();
        using var reader = new StringReader(trimmed);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0) continue;
            var item = JsonSerializer.Deserialize<SuiteCase>(line, JsonOptions());
            if (item != null) results.Add(item);
        }
        return results;
    }

    private static bool LooksLikeCsv(string trimmed, string? pathHint)
    {
        if (pathHint?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true
            || pathHint?.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) == true)
            return false;
        var firstLine = trimmed.Split('\n')[0].Trim();
        return firstLine.Contains(',')
               && firstLine.Contains("id", StringComparison.OrdinalIgnoreCase)
               && (firstLine.Contains("actual", StringComparison.OrdinalIgnoreCase)
                   || firstLine.Contains("expected", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SuiteRunResult> RunAsync(
        IEnumerable<SuiteCase> cases,
        CancellationToken cancellationToken = default)
    {
        var caseList = cases.ToList();
        var started = DateTimeOffset.UtcNow;
        var results = new SuiteCaseResult[caseList.Count];
        var parallelism = Math.Max(1, _options.MaxDegreeOfParallelism);
        using var gate = new SemaphoreSlim(parallelism);

        var tasks = caseList.Select(async (c, index) =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var request = ToRequest(c);
                var eval = await _evaluationService.EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
                results[index] = new SuiteCaseResult
                {
                    Id = string.IsNullOrEmpty(c.Id) ? $"case-{index + 1}" : c.Id,
                    Question = c.Question,
                    Expected = c.Expected,
                    Actual = c.Actual,
                    Score = eval.Score,
                    Passed = eval.IsPassed,
                    Details = eval.Details,
                    RiskLevel = eval.RiskLevel,
                    MetricName = eval.MetricName,
                    GroundednessScore = eval.GroundednessScore,
                    HallucinationRate = eval.HallucinationRate,
                    Usage = eval.Usage
                };
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var completed = DateTimeOffset.UtcNow;
        var passed = results.Count(r => r.Passed);
        var totalUsage = TokenUsage.Combine(results.Select(r => r.Usage).ToArray());
        if (totalUsage.PromptTokens == null && totalUsage.CompletionTokens == null && totalUsage.TotalTokens == null
            && totalUsage.EstimatedCostUsd == null)
            totalUsage = null;

        return new SuiteRunResult
        {
            StartedAt = started,
            CompletedAt = completed,
            Total = results.Length,
            Passed = passed,
            Failed = results.Length - passed,
            PassRate = results.Length == 0 ? 1.0 : (double)passed / results.Length,
            Cases = results,
            TotalUsage = totalUsage
        };
    }

    /// <summary>
    /// Writes report.json, report.html, report.md, and report.csv into <paramref name="outputDirectory"/>.
    /// </summary>
    public async Task WriteReportsAsync(SuiteRunResult result, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "report.json");
        var htmlPath = Path.Combine(outputDirectory, "report.html");
        var mdPath = Path.Combine(outputDirectory, "report.md");
        var csvPath = Path.Combine(outputDirectory, "report.csv");

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(htmlPath, HtmlResult.Write(result), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(mdPath, MarkdownReportWriter.Write(result), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(csvPath, CsvReportWriter.Write(result), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads a previous suite <c>report.json</c> as a baseline for comparison.</summary>
    public static async Task<SuiteRunResult> LoadBaselineAsync(string reportJsonPath, CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(reportJsonPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SuiteRunResult>(text, JsonOptions())
               ?? throw new InvalidOperationException($"Could not deserialize baseline from '{reportJsonPath}'.");
    }

    private EvaluationRequest ToRequest(SuiteCase c)
    {
        var request = new EvaluationRequest
        {
            Question = c.Question,
            AiResponse = c.Actual,
            GoldenOutput = c.Expected,
            MatchingType = string.IsNullOrWhiteSpace(c.MatchingType) ? "exact" : c.MatchingType,
            PassThreshold = c.Threshold ?? _options.DefaultPassThreshold,
            IsReferenceDoc = c.IsReferenceDoc,
            ReferenceDocuments = c.ReferenceDocuments,
            Schema = c.Schema,
            Endpoint = c.Endpoint ?? _options.Endpoint,
            ProviderType = ParseProvider(c.Provider) ?? _options.DefaultProvider,
            EvaluationType = ParseEvalType(c.EvaluationType),
            Configuration = _options.ToConfigurationDictionary()
        };

        if (!string.IsNullOrWhiteSpace(c.Model))
        {
            request.ModelName = c.Model;
            request.Configuration["Model"] = c.Model!;
        }

        AdvancedEvaluationService.ApplyModelNameToConfiguration(request);
        return request;
    }

    private static ProviderType? ParseProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Enum.TryParse<ProviderType>(value, ignoreCase: true, out var p) ? p : null;
    }

    private static EvaluationType ParseEvalType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return EvaluationType.DirectEvaluation;
        return Enum.TryParse<EvaluationType>(value, ignoreCase: true, out var t)
            ? t
            : EvaluationType.DirectEvaluation;
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>Minimal CSV parser for suite datasets (header row required).</summary>
internal static class CsvDatasetParser
{
    public static IReadOnlyList<SuiteCase> Parse(string text)
    {
        var lines = SplitLines(text);
        if (lines.Count == 0) return Array.Empty<SuiteCase>();

        var header = ParseRow(lines[0]);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
            map[header[i].Trim()] = i;

        var cases = new List<SuiteCase>();
        for (var r = 1; r < lines.Count; r++)
        {
            if (string.IsNullOrWhiteSpace(lines[r])) continue;
            var cols = ParseRow(lines[r]);
            string Get(string name) =>
                map.TryGetValue(name, out var idx) && idx < cols.Count ? cols[idx] : string.Empty;

            var c = new SuiteCase
            {
                Id = Get("id"),
                Question = Get("question"),
                Actual = Get("actual"),
                Expected = Get("expected"),
                EvaluationType = string.IsNullOrWhiteSpace(Get("evaluationType")) ? "DirectEvaluation" : Get("evaluationType"),
                MatchingType = string.IsNullOrWhiteSpace(Get("matchingType")) ? "exact" : Get("matchingType"),
                Provider = NullIfEmpty(Get("provider")),
                Endpoint = NullIfEmpty(Get("endpoint")),
                Model = NullIfEmpty(Get("model")),
                Schema = NullIfEmpty(Get("schema")),
                IsReferenceDoc = bool.TryParse(Get("isReferenceDoc"), out var ird) && ird,
                Tags = ParseTags(Get("tags"))
            };

            if (double.TryParse(Get("threshold"), NumberStyles.Any, CultureInfo.InvariantCulture, out var th))
                c.Threshold = th;

            cases.Add(c);
        }

        return cases;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static List<string>? ParseTags(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var tags = raw
            .Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .ToList();
        return tags.Count == 0 ? null : tags;
    }

    private static List<string> SplitLines(string text)
    {
        var list = new List<string>();
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
            list.Add(line);
        return list;
    }

    private static List<string> ParseRow(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else
            {
                if (ch == '"') inQuotes = true;
                else if (ch == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(ch);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }
}

internal static class MarkdownReportWriter
{
    public static string Write(SuiteRunResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# STAF.LLMEval Suite Report");
        sb.AppendLine();
        sb.AppendLine($"- **Started:** {result.StartedAt:u}");
        sb.AppendLine($"- **Completed:** {result.CompletedAt:u}");
        sb.AppendLine($"- **Pass rate:** {result.PassRate:P1} ({result.Passed}/{result.Total})");
        if (result.TotalUsage?.TotalTokens != null)
            sb.AppendLine($"- **Tokens:** {result.TotalUsage.TotalTokens} (prompt {result.TotalUsage.PromptTokens}, completion {result.TotalUsage.CompletionTokens})");
        if (result.TotalUsage?.EstimatedCostUsd != null)
            sb.AppendLine($"- **Est. cost (USD):** {result.TotalUsage.EstimatedCostUsd:0.######}");
        sb.AppendLine();
        sb.AppendLine("| Id | Passed | Score | Metric | Question | Details |");
        sb.AppendLine("|----|--------|-------|--------|----------|---------|");
        foreach (var c in result.Cases)
        {
            sb.AppendLine(
                $"| {Escape(c.Id)} | {(c.Passed ? "PASS" : "FAIL")} | {c.Score:0.###} | {Escape(c.MetricName)} | {Escape(Truncate(c.Question, 80))} | {Escape(Truncate(c.Details, 120))} |");
        }
        return sb.ToString();
    }

    private static string Truncate(string? s, int max)
    {
        s ??= string.Empty;
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }

    private static string Escape(string? s) =>
        (s ?? string.Empty).Replace("|", "\\|");
}

internal static class CsvReportWriter
{
    public static string Write(SuiteRunResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,passed,score,metric,question,expected,actual,details,promptTokens,completionTokens,totalTokens");
        foreach (var c in result.Cases)
        {
            sb.Append(Escape(c.Id)).Append(',');
            sb.Append(c.Passed ? "true" : "false").Append(',');
            sb.Append(c.Score.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Escape(c.MetricName)).Append(',');
            sb.Append(Escape(c.Question)).Append(',');
            sb.Append(Escape(c.Expected)).Append(',');
            sb.Append(Escape(c.Actual)).Append(',');
            sb.Append(Escape(c.Details)).Append(',');
            sb.Append(c.Usage?.PromptTokens?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',');
            sb.Append(c.Usage?.CompletionTokens?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',');
            sb.AppendLine(c.Usage?.TotalTokens?.ToString(CultureInfo.InvariantCulture) ?? "");
        }
        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
