namespace LLMEval.Tests;

public class Phase3AssertionMessageTests
{
    [Fact]
    public void ShouldPass_IncludesMetricGroundingUsageAndBecause()
    {
        var result = new EvaluationResult
        {
            Score = 0.4,
            IsPassed = false,
            MetricName = "exact",
            RiskLevel = "Low",
            GroundednessScore = 0.4,
            HallucinationRate = 0.6,
            Details = "mismatch",
            PartiallySupportedStatements = new[] { "partial-a" },
            Usage = new TokenUsage { TotalTokens = 42, EstimatedCostUsd = 0.001234m }
        };

        var ex = Assert.Throws<LLMEvalAssertionException>(() =>
            result.ShouldPass(because: "CI smoke gate"));

        Assert.Contains("Metric=exact", ex.Message);
        Assert.Contains("Groundedness=0.4", ex.Message);
        Assert.Contains("HallucinationRate=0.6", ex.Message);
        Assert.Contains("tokens=42", ex.Message);
        Assert.Contains("PartiallySupported: partial-a", ex.Message);
        Assert.Contains("Because: CI smoke gate", ex.Message);
        Assert.Contains("Details=mismatch", ex.Message);
        Assert.Same(result, ex.Result);
    }

    [Fact]
    public void ShouldMeetPassRate_ThrowsWithFailedCaseSummary()
    {
        var suite = new SuiteRunResult
        {
            Total = 2,
            Passed = 1,
            Failed = 1,
            PassRate = 0.5,
            Cases = new[]
            {
                new SuiteCaseResult { Id = "ok", Passed = true, Score = 1, MetricName = "exact", Details = "pass" },
                new SuiteCaseResult { Id = "bad", Passed = false, Score = 0, MetricName = "keyword", Details = "no keywords" }
            }
        };

        var ex = Assert.Throws<LLMEvalAssertionException>(() =>
            suite.ShouldMeetPassRate(0.9, because: "release gate"));

        Assert.Contains("90.0", ex.Message);
        Assert.Contains("bad", ex.Message);
        Assert.Contains("no keywords", ex.Message);
        Assert.Contains("Because: release gate", ex.Message);
        Assert.Same(suite, ex.SuiteResult);
    }

    [Fact]
    public void ShouldMeetPassRate_PassesWhenMet()
    {
        var suite = new SuiteRunResult
        {
            Total = 1,
            Passed = 1,
            Failed = 0,
            PassRate = 1.0,
            Cases = new[] { new SuiteCaseResult { Id = "a", Passed = true, Score = 1 } }
        };

        Assert.Same(suite, suite.ShouldMeetPassRate(0.9));
    }
}

public class Phase3TraitAndTagTests
{
    [Fact]
    public void EvalTraits_Constants_AreStableForFilters()
    {
        Assert.Equal("Category", EvalTraits.Category);
        Assert.Equal("LLMEval", EvalTraits.LLMEval);
        Assert.Equal("Kind", EvalTraits.Kind);
        Assert.Equal("Suite", EvalTraits.Suite);
        Assert.Equal("Smoke", EvalTraits.Smoke);
    }

    [Fact]
    public void FilterByTags_AnyMatch_Default()
    {
        var cases = new[]
        {
            new SuiteCase { Id = "a", Tags = new List<string> { "smoke", "ci" } },
            new SuiteCase { Id = "b", Tags = new List<string> { "nightly" } },
            new SuiteCase { Id = "c", Tags = null }
        };

        var filtered = cases.FilterByTags("smoke");
        Assert.Single(filtered);
        Assert.Equal("a", filtered[0].Id);
    }

    [Fact]
    public void FilterByTags_RequireAll()
    {
        var cases = new[]
        {
            new SuiteCase { Id = "a", Tags = new List<string> { "smoke", "ci" } },
            new SuiteCase { Id = "b", Tags = new List<string> { "smoke" } }
        };

        var filtered = cases.FilterByTags(requireAll: true, "smoke", "ci");
        Assert.Single(filtered);
        Assert.Equal("a", filtered[0].Id);
    }

    [Fact]
    public void ParseDataset_Json_ReadsTags()
    {
        var json = """
        [
          { "id": "t1", "actual": "x", "expected": "x", "matchingType": "exact", "tags": ["smoke", "ci"] }
        ]
        """;
        var cases = EvaluationSuite.ParseDataset(json);
        Assert.Equal(new[] { "smoke", "ci" }, cases[0].Tags);
    }

    [Fact]
    public void ParseDataset_Csv_ReadsTagsColumn()
    {
        var csv = """
        id,actual,expected,matchingType,tags
        t1,x,x,exact,smoke;ci
        t2,y,y,exact,nightly
        """;
        var cases = EvaluationSuite.ParseDataset(csv.Trim(), "cases.csv");
        Assert.Equal(2, cases.Count);
        Assert.Equal(new[] { "smoke", "ci" }, cases[0].Tags);
        Assert.Equal(new[] { "nightly" }, cases[1].Tags);
    }

    [Fact]
    public async Task Suite_FilterThenRun_SmokeOnly()
    {
        var cases = new[]
        {
            new SuiteCase
            {
                Id = "smoke-exact",
                Actual = "Paris",
                Expected = "Paris",
                MatchingType = "exact",
                Threshold = 1.0,
                Tags = new List<string> { "smoke" }
            },
            new SuiteCase
            {
                Id = "skip-me",
                Actual = "London",
                Expected = "Paris",
                MatchingType = "exact",
                Threshold = 1.0,
                Tags = new List<string> { "nightly" }
            }
        };

        var filtered = cases.FilterByTags(EvalTraits.Smoke);
        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));
        var report = await suite.RunAsync(filtered);

        Assert.Equal(1, report.Total);
        Assert.Equal("smoke-exact", report.Cases[0].Id);
        report.ShouldMeetPassRate(1.0);
    }
}

public class Phase3ReportDirHelperTests
{
    [Fact]
    public void ResolveReportDirectory_UsesEnvOverride()
    {
        var previous = Environment.GetEnvironmentVariable("LLMEVAL_REPORT_DIR");
        try
        {
            var custom = Path.Combine(Path.GetTempPath(), "llmeval-phase3-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("LLMEVAL_REPORT_DIR", custom);
            Assert.Equal(custom, ReportPaths.ResolveReportDirectory("fallback"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LLMEVAL_REPORT_DIR", previous);
        }
    }

    [Fact]
    public void ResolveReportDirectory_RelativeUsesGitHubWorkspace()
    {
        var previousReport = Environment.GetEnvironmentVariable("LLMEVAL_REPORT_DIR");
        var previousWs = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        try
        {
            var ws = Path.Combine(Path.GetTempPath(), "llmeval-ws-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ws);
            Environment.SetEnvironmentVariable("GITHUB_WORKSPACE", ws);
            Environment.SetEnvironmentVariable("LLMEVAL_REPORT_DIR", "artifacts/llmeval");
            var resolved = ReportPaths.ResolveReportDirectory("fallback");
            Assert.Equal(Path.GetFullPath(Path.Combine(ws, "artifacts", "llmeval")), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LLMEVAL_REPORT_DIR", previousReport);
            Environment.SetEnvironmentVariable("GITHUB_WORKSPACE", previousWs);
        }
    }
}
