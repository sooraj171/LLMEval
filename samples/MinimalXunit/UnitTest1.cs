using LLMEval;

namespace MinimalXunit;

public class DirectEvaluationSamples
{
    [Fact]
    [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
    [Trait(EvalTraits.Kind, EvalTraits.Direct)]
    [Trait(EvalTraits.Tag, EvalTraits.Smoke)]
    public async Task ExactMatch_ShouldPass()
    {
        // Simulate your app's LLM output
        string aiResponse = "Paris";

        var result = await Eval.Direct()
            .Exact(actual: aiResponse, expected: "Paris")
            .WithThreshold(1.0)
            .EvaluateAsync();

        result.ShouldPass(because: "exact capital match");
    }

    [Fact]
    [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
    [Trait(EvalTraits.Kind, EvalTraits.Direct)]
    public async Task KeywordMatch_ShouldScoreAboveThreshold()
    {
        var result = await Eval.Direct()
            .Keyword(
                actual: "The capital of France is Paris",
                expected: "capital France Paris")
            .WithThreshold(0.5)
            .EvaluateAsync();

        result.ShouldPass().ShouldScoreAbove(0.49, because: "keyword coverage");
    }

    [Fact]
    [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
    [Trait(EvalTraits.Kind, EvalTraits.Direct)]
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
    [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
    [Trait(EvalTraits.Kind, EvalTraits.Suite)]
    [Trait(EvalTraits.Tag, EvalTraits.Smoke)]
    public async Task RunDataset_WritesHtmlJsonMarkdownCsvReports()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "cases.json");
        var cases = await EvaluationSuite.LoadAsync(path);
        var service = new AdvancedEvaluationService(new AiProviderFactory());
        var suite = new EvaluationSuite(service);

        var report = await suite.RunAsync(cases);
        var outDir = ReportPaths.ResolveReportDirectory(
            Path.Combine(Path.GetTempPath(), "llmeval-sample-report"));
        await suite.WriteReportsAsync(report, outDir);

        report.ShouldMeetPassRate(0.9, because: "CI pass-rate threshold");
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
    [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
    [Trait(EvalTraits.Kind, EvalTraits.Baseline)]
    public async Task RunDataset_ComparedToBaseline_HasNoRegressions()
    {
        var casesPath = Path.Combine(AppContext.BaseDirectory, "cases.json");
        var baselinePath = Path.Combine(AppContext.BaseDirectory, "baseline-report.json");
        var cases = await EvaluationSuite.LoadAsync(casesPath);
        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));

        var current = await suite.RunAsync(cases);
        var diff = await BaselineComparer.CompareToBaselineFileAsync(current, baselinePath);

        var outDir = ReportPaths.ResolveReportDirectory(
            Path.Combine(Path.GetTempPath(), "llmeval-baseline-diff"));
        await BaselineComparer.WriteDiffReportAsync(diff, outDir);

        Assert.False(diff.HasRegressions, diff.ToSummary());
    }

    [Fact]
    [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
    [Trait(EvalTraits.Kind, EvalTraits.Suite)]
    public async Task RunDataset_FilterBySmokeTag()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "cases.json");
        var cases = await EvaluationSuite.LoadAsync(path);
        var smoke = cases.FilterByTags(EvalTraits.Smoke);
        Assert.NotEmpty(smoke);

        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));
        var report = await suite.RunAsync(smoke);
        report.ShouldMeetPassRate(1.0);
    }
}

public class CsvDatasetSample
{
    [Fact]
    [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
    [Trait(EvalTraits.Kind, EvalTraits.Suite)]
    public async Task LoadCsvDataset_AndRun()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "cases.csv");
        var cases = await EvaluationSuite.LoadAsync(path);
        Assert.NotEmpty(cases);

        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));
        var report = await suite.RunAsync(cases);
        report.ShouldMeetPassRate(0.5, because: "CSV dataset gate");
    }
}
