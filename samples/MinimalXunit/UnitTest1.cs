using LLMEval;

namespace MinimalXunit;

public class DirectEvaluationSamples
{
    [Fact]
    public async Task ExactMatch_ShouldPass()
    {
        // Simulate your app's LLM output
        string aiResponse = "Paris";

        var result = await Eval.Direct()
            .Exact(actual: aiResponse, expected: "Paris")
            .WithThreshold(1.0)
            .EvaluateAsync();

        result.ShouldPass();
    }

    [Fact]
    public async Task KeywordMatch_ShouldScoreAboveThreshold()
    {
        var result = await Eval.Direct()
            .Keyword(
                actual: "The capital of France is Paris",
                expected: "capital France Paris")
            .WithThreshold(0.5)
            .EvaluateAsync();

        result.ShouldPass().ShouldScoreAbove(0.49);
    }

    [Fact]
    public async Task JsonAndSchema_ShouldPass()
    {
        var json = await Eval.Direct()
            .Json("""{"city":"Paris"}""")
            .EvaluateAsync();
        json.ShouldPass();

        var schema = """
        {
          "type": "object",
          "required": ["city"],
          "properties": { "city": { "type": "string" } }
        }
        """;
        var validated = await Eval.Direct()
            .Schema("""{"city":"Paris"}""", schema)
            .EvaluateAsync();
        validated.ShouldPass();
    }
}

public class SuiteReportSample
{
    [Fact]
    public async Task RunDataset_WritesHtmlJsonMarkdownCsvReports()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "cases.json");
        var cases = await EvaluationSuite.LoadAsync(path);
        var service = new AdvancedEvaluationService(new AiProviderFactory());
        var suite = new EvaluationSuite(service);

        var report = await suite.RunAsync(cases);
        var outDir = Path.Combine(Path.GetTempPath(), "llmeval-sample-report");
        await suite.WriteReportsAsync(report, outDir);

        Assert.True(report.MeetsPassRate(0.5));
        Assert.True(File.Exists(Path.Combine(outDir, "report.html")));
        Assert.True(File.Exists(Path.Combine(outDir, "report.json")));
        Assert.True(File.Exists(Path.Combine(outDir, "report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "report.csv")));
    }

    /// <summary>
    /// CI-friendly golden baseline check: compare the latest suite run to a checked-in baseline report.
    /// Fails when previously-passing cases regress.
    /// </summary>
    [Fact]
    public async Task RunDataset_ComparedToBaseline_HasNoRegressions()
    {
        var casesPath = Path.Combine(AppContext.BaseDirectory, "cases.json");
        var baselinePath = Path.Combine(AppContext.BaseDirectory, "baseline-report.json");
        var cases = await EvaluationSuite.LoadAsync(casesPath);
        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));

        var current = await suite.RunAsync(cases);
        var diff = await BaselineComparer.CompareToBaselineFileAsync(current, baselinePath);

        var outDir = Path.Combine(Path.GetTempPath(), "llmeval-baseline-diff");
        await BaselineComparer.WriteDiffReportAsync(diff, outDir);

        Assert.False(diff.HasRegressions, diff.ToSummary());
    }
}

public class CsvDatasetSample
{
    [Fact]
    public async Task LoadCsvDataset_AndRun()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "cases.csv");
        var cases = await EvaluationSuite.LoadAsync(path);
        Assert.NotEmpty(cases);

        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));
        var report = await suite.RunAsync(cases);
        Assert.True(report.MeetsPassRate(0.5));
    }
}
