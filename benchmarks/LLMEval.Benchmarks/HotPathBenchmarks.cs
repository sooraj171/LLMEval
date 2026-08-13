using BenchmarkDotNet.Attributes;

namespace LLMEval.Benchmarks;

/// <summary>Hot-path Direct metrics (no network).</summary>
[MemoryDiagnoser(false)]
public class MetricBenchmarks
{
    private readonly ExactMatchMetric _exact = new();
    private readonly KeywordMatchMetric _keyword = new();
    private readonly SemanticSimilarityMetric _semantic = new();
    private MetricContext _exactCtx = null!;
    private MetricContext _keywordCtx = null!;
    private MetricContext _semanticCtx = null!;

    [GlobalSetup]
    public void Setup()
    {
        _exactCtx = new MetricContext
        {
            Actual = "Paris",
            Expected = "Paris",
            PassThreshold = 1.0
        };
        _keywordCtx = new MetricContext
        {
            Actual = "The capital of France is Paris and it is beautiful.",
            Expected = "capital France Paris",
            PassThreshold = 0.5
        };
        _semanticCtx = new MetricContext
        {
            Actual = "Paris is the capital city of France in Europe.",
            Expected = "The capital of France is Paris.",
            PassThreshold = 0.2
        };
    }

    [Benchmark]
    public Task<MetricResult> ExactMatch() => _exact.EvaluateAsync(_exactCtx);

    [Benchmark]
    public Task<MetricResult> KeywordMatch() => _keyword.EvaluateAsync(_keywordCtx);

    [Benchmark]
    public Task<MetricResult> SemanticTfidf() => _semantic.EvaluateAsync(_semanticCtx);
}

/// <summary>Dataset parse + STAF HTML report generation.</summary>
[MemoryDiagnoser(false)]
public class SuiteReportBenchmarks
{
    private string _json = null!;
    private SuiteRunResult _suite = null!;

    [GlobalSetup]
    public void Setup()
    {
        var cases = Enumerable.Range(1, 50).Select(i => new SuiteCase
        {
            Id = $"case-{i}",
            Question = $"What is item {i}?",
            Actual = i % 2 == 0 ? "yes" : "no",
            Expected = "yes",
            MatchingType = "exact",
            Threshold = 1.0
        }).ToList();

        _json = System.Text.Json.JsonSerializer.Serialize(cases);

        _suite = new SuiteRunResult
        {
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-2),
            CompletedAt = DateTimeOffset.UtcNow,
            Total = cases.Count,
            Passed = cases.Count / 2,
            Failed = cases.Count - cases.Count / 2,
            PassRate = 0.5,
            Cases = cases.Select(c => new SuiteCaseResult
            {
                Id = c.Id,
                Question = c.Question,
                Actual = c.Actual,
                Expected = c.Expected,
                Score = string.Equals(c.Actual, c.Expected, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                Passed = string.Equals(c.Actual, c.Expected, StringComparison.OrdinalIgnoreCase),
                Details = "bench",
                MetricName = "exact"
            }).ToList()
        };
    }

    [Benchmark]
    public IReadOnlyList<SuiteCase> ParseJsonDataset() =>
        EvaluationSuite.ParseDataset(_json, "cases.json");

    [Benchmark]
    public string WriteHtmlReport() => HtmlResult.Write(_suite);
}
