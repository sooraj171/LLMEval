# Minimal xUnit sample

Demonstrates STAF.LLMEval **v3.0.0** DX: fluent `Eval.Direct()`, rich assertions, `EvalTraits`, JSON/CSV suites with tags, Markdown/CSV reports, golden **baseline comparison**, and CI-friendly `LLMEVAL_REPORT_DIR` — **no API keys required**.

```bash
dotnet test samples/MinimalXunit/MinimalXunit.csproj
dotnet test samples/MinimalXunit/MinimalXunit.csproj --filter "Category=LLMEval&Tag=Smoke"
```

| Test | What it shows |
|------|----------------|
| `ExactMatch_ShouldPass` | `Eval.Direct().Exact(...).ShouldPass(because:)` + traits |
| `KeywordMatch_ShouldScoreAboveThreshold` | Keyword Direct + `ShouldScoreAbove` |
| `JsonAndSchema_ShouldPass` | JSON validity + schema metrics |
| `RunDataset_WritesHtmlJsonMarkdownCsvReports` | Suite → reports + `ShouldMeetPassRate(0.9)` |
| `RunDataset_ComparedToBaseline_HasNoRegressions` | `BaselineComparer` vs `baseline-report.json` |
| `RunDataset_FilterBySmokeTag` | `FilterByTags("smoke")` |
| `LoadCsvDataset_AndRun` | CSV dataset loading (with tags column) |

Set `LLMEVAL_REPORT_DIR=artifacts/llmeval` in CI so suite reports land in a folder you can upload as an artifact. See [`samples/ci`](../ci).

For LLM-as-judge or grounding, use `Eval.Judge()` / `Eval.Grounding()` or `services.AddLLMEval(...)` and set secrets via environment variables (see root [README.md](../../README.md) and [LLMEval/Readme.md](../../LLMEval/Readme.md)).
