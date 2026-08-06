# Minimal xUnit sample

Demonstrates STAF.LLMEval **v2.0** DX: fluent `Eval.Direct()`, assertions, and suite HTML/JSON reports — **no API keys required**.

```bash
dotnet test samples/MinimalXunit/MinimalXunit.csproj
```

| Test | What it shows |
|------|----------------|
| `ExactMatch_ShouldPass` | `Eval.Direct().Exact(...).ShouldPass()` |
| `KeywordMatch_ShouldScoreAboveThreshold` | Keyword Direct + `ShouldScoreAbove` |
| `RunDataset_WritesHtmlAndJsonReports` | `EvaluationSuite` + `cases.json` → `report.html` / `report.json` |

For LLM-as-judge or grounding, use `Eval.Judge()` / `Eval.Grounding()` or `services.AddLLMEval(...)` and set secrets via environment variables (see root [README.md](../../README.md) and [LLMEval/Readme.md](../../LLMEval/Readme.md)).
