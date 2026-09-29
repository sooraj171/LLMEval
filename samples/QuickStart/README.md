# QuickStart sample

Clone the repo and run — **no API keys, no extra setup**:

```bash
dotnet test samples/QuickStart/QuickStart.csproj
```

Uses only `Eval.Direct()` matchers (`Exact`, `Keyword`, `Json`) — STAF.LLMEval **3.2.1**, no API keys. For suites, traits, and baseline CI checks see [`samples/MinimalXunit`](../MinimalXunit). For embeddings vs TF-IDF see the [root README](../../README.md). For a live LLM-as-judge example see [`samples/OpenAIJudge`](../OpenAIJudge).
