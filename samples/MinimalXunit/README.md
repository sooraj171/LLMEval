# Minimal xUnit sample

Demonstrates STAF.LLMEval **v2.1.0** DX: fluent `Eval.Direct()`, assertions, JSON/CSV suites, Markdown/CSV reports, and golden **baseline comparison** — **no API keys required**.

```bash
dotnet test samples/MinimalXunit/MinimalXunit.csproj
```

| Test | What it shows |
|------|----------------|
| `ExactMatch_ShouldPass` | `Eval.Direct().Exact(...).ShouldPass()` |
| `KeywordMatch_ShouldScoreAboveThreshold` | Keyword Direct + `ShouldScoreAbove` |
| `JsonAndSchema_ShouldPass` | JSON validity + schema metrics |
| `RunDataset_WritesHtmlJsonMarkdownCsvReports` | Suite → `report.html` / `.json` / `.md` / `.csv` |
| `RunDataset_ComparedToBaseline_HasNoRegressions` | `BaselineComparer` vs `baseline-report.json` |
| `LoadCsvDataset_AndRun` | CSV dataset loading |

For LLM-as-judge or grounding, use `Eval.Judge()` / `Eval.Grounding()` or `services.AddLLMEval(...)` and set secrets via environment variables (see root [README.md](../../README.md) and [LLMEval/Readme.md](../../LLMEval/Readme.md)).
