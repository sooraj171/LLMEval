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
}

public class SuiteReportSample
{
    [Fact]
    public async Task RunDataset_WritesHtmlAndJsonReports()
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
    }
}
