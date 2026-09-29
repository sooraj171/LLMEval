using LLMEval;

namespace LLMEval.Tests;

public class RunHistoryTests
{
    [Fact]
    public async Task WriteReports_Default_DoesNotWriteHistoryFile()
    {
        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()));
        var report = await suite.RunAsync(new[]
        {
            new SuiteCase { Id = "1", Actual = "Paris", Expected = "Paris", MatchingType = "exact", Threshold = 1.0 }
        });

        var dir = Path.Combine(Path.GetTempPath(), "llmeval-nohist-" + Guid.NewGuid().ToString("N"));
        try
        {
            await suite.WriteReportsAsync(report, dir);
            Assert.False(File.Exists(Path.Combine(dir, "history.jsonl")));
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "report.html"));
            Assert.DoesNotContain("Pass-rate trend", html);
            Assert.DoesNotContain("<svg", html);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteReports_EnableRunHistory_AppendsJsonl_AndSvgTrend()
    {
        var options = new LLMEvalOptions { EnableRunHistory = true, TrendHistoryLength = 10 };
        var suite = new EvaluationSuite(new AdvancedEvaluationService(new AiProviderFactory()), options);
        var pass = new[]
        {
            new SuiteCase { Id = "1", Actual = "Paris", Expected = "Paris", MatchingType = "exact", Threshold = 1.0 }
        };
        var fail = new[]
        {
            new SuiteCase { Id = "1", Actual = "London", Expected = "Paris", MatchingType = "exact", Threshold = 1.0 }
        };

        var dir = Path.Combine(Path.GetTempPath(), "llmeval-hist-" + Guid.NewGuid().ToString("N"));
        try
        {
            var r1 = await suite.RunAsync(pass);
            await suite.WriteReportsAsync(r1, dir);
            var r2 = await suite.RunAsync(fail);
            await suite.WriteReportsAsync(r2, dir);

            var historyPath = Path.Combine(dir, "history.jsonl");
            Assert.True(File.Exists(historyPath));
            var lines = (await File.ReadAllLinesAsync(historyPath)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            Assert.Equal(2, lines.Length);
            Assert.Contains("passRate", lines[0]);
            Assert.Contains("metricAverages", lines[0]);

            var html = await File.ReadAllTextAsync(Path.Combine(dir, "report.html"));
            Assert.Contains("Pass-rate trend", html);
            Assert.Contains("<svg", html);
            Assert.Contains("polyline", html);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunHistoryStore_RespectsTrendHistoryLength()
    {
        var path = Path.Combine(Path.GetTempPath(), "llmeval-histfile-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            for (var i = 0; i < 5; i++)
            {
                var result = new SuiteRunResult
                {
                    CompletedAt = DateTimeOffset.UtcNow,
                    Total = 2,
                    Passed = i < 3 ? 2 : 1,
                    Failed = i < 3 ? 0 : 1,
                    PassRate = i < 3 ? 1.0 : 0.5,
                    Cases = Array.Empty<SuiteCaseResult>()
                };
                var slice = await RunHistoryStore.AppendAndReadAsync(path, result, maxEntries: 3);
                Assert.True(slice.Count <= 3);
            }

            var last = await RunHistoryStore.ReadLastAsync(path, 3);
            Assert.Equal(3, last.Count);
            Assert.Equal(0.5, last[^1].PassRate);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void HtmlResult_Write_OverloadWithoutHistory_UnchangedSkin()
    {
        var html = HtmlResult.Write(new SuiteRunResult
        {
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Total = 1,
            Passed = 1,
            PassRate = 1,
            Cases = new[]
            {
                new SuiteCaseResult { Id = "x", Actual = "a", Expected = "a", Passed = true, Score = 1, MetricName = "exact" }
            }
        });
        Assert.Contains("headBk", html);
        Assert.DoesNotContain("Pass-rate trend", html);
    }
}
