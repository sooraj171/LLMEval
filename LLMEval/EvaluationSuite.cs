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

    /// <summary>Returns true when pass rate meets or exceeds <paramref name="minimumPassRate"/> (0–1).</summary>
    public bool MeetsPassRate(double minimumPassRate) => PassRate >= minimumPassRate;
}

/// <summary>Loads JSON/JSONL datasets and runs batch evaluations with optional reports.</summary>
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
                    RiskLevel = eval.RiskLevel
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
        return new SuiteRunResult
        {
            StartedAt = started,
            CompletedAt = completed,
            Total = results.Length,
            Passed = passed,
            Failed = results.Length - passed,
            PassRate = results.Length == 0 ? 1.0 : (double)passed / results.Length,
            Cases = results
        };
    }

    public async Task WriteReportsAsync(SuiteRunResult result, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "report.json");
        var htmlPath = Path.Combine(outputDirectory, "report.html");

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(htmlPath, HtmlReportWriter.Write(result), cancellationToken).ConfigureAwait(false);
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

internal static class HtmlReportWriter
{
    public static string Write(SuiteRunResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>STAF.LLMEval Report</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;margin:2rem}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ccc;padding:.5rem;text-align:left}th{background:#f5f5f5}.pass{color:#0a7}.fail{color:#c00}pre{white-space:pre-wrap}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>STAF.LLMEval Suite Report</h1>");
        sb.AppendLine($"<p>Started: {Encode(result.StartedAt.ToString("u"))}<br/>Completed: {Encode(result.CompletedAt.ToString("u"))}</p>");
        sb.AppendLine($"<p><strong>Pass rate:</strong> {result.PassRate:P1} ({result.Passed}/{result.Total})</p>");
        sb.AppendLine("<table><thead><tr><th>Id</th><th>Passed</th><th>Score</th><th>Question / Prompt</th><th>Expected</th><th>Actual</th><th>Details</th></tr></thead><tbody>");
        foreach (var c in result.Cases)
        {
            var cls = c.Passed ? "pass" : "fail";
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{Encode(c.Id)}</td>");
            sb.AppendLine($"<td class=\"{cls}\">{(c.Passed ? "PASS" : "FAIL")}</td>");
            sb.AppendLine($"<td>{c.Score:0.###}</td>");
            sb.AppendLine($"<td><pre>{Encode(c.Question)}</pre></td>");
            sb.AppendLine($"<td><pre>{Encode(c.Expected)}</pre></td>");
            sb.AppendLine($"<td><pre>{Encode(c.Actual)}</pre></td>");
            sb.AppendLine($"<td><pre>{Encode(c.Details)}</pre></td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table></body></html>");
        return sb.ToString();
    }

    private static string Encode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
