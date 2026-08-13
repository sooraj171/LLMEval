# STAF.LLMEval best practices

Practical guidance for evaluating LLM outputs in unit tests and CI.

## Prefer Direct evaluation when you can

Use `Eval.Direct()` with exact / keyword / JSON / schema metrics for golden answers. No API keys, fast, deterministic.

```csharp
var result = await Eval.Direct()
    .Exact(actual, expected)
    .WithThreshold(1.0)
    .EvaluateAsync();
result.ShouldPass(because: "capital city");
```

Reserve **LLM-as-judge** and **Grounding** for semantic quality and RAG hallucination checks.

## Assert for humans and CI

- Prefer `ShouldPass(because: …)`, `ShouldScoreAbove`, `ShouldBeGrounded`, `ShouldMeetPassRate`
- Tag tests with `EvalTraits` so CI can filter: `--filter "Category=LLMEval&Tag=Smoke"`
- Tag suite cases and use `FilterByTags` for smoke vs full suites

## Suites and baselines

1. Keep datasets in JSON / JSONL / CSV under source control.
2. Write reports to `ReportPaths.ResolveReportDirectory(...)` (honors `LLMEVAL_REPORT_DIR`).
3. Check in a golden `baseline-report.json` and fail on regressions:

```csharp
var diff = await BaselineComparer.CompareToBaselineFileAsync(report, "baseline-report.json");
Assert.False(diff.HasRegressions, diff.ToSummary());
```

4. Gate CI with `report.ShouldMeetPassRate(0.9)`.

Official templates: [`samples/ci`](../samples/ci).

## Provider configuration

- Set `Temperature=0` for judge/grounding in CI.
- Put keys in environment / secret stores — never in committed cases.
- For Azure OpenAI, `Model` is the **deployment name**.
- Prefer DI: `services.AddLLMEval(configuration)` binding section `LLMEval`.

## Metrics

- Register custom metrics via `MetricRegistry` / `AddLLMEvalMetric<T>()` instead of forking Core.
- Semantic Direct matching is **TF-IDF**, not embeddings — choose thresholds accordingly.
- Unknown `MatchingType` values fail clearly; do not rely on silent exact fallback.

## Packages

- Apps/tests: `dotnet add package STAF.LLMEval` (meta).
- Library authors needing contracts only: `STAF.LLMEval.Abstractions`.
- Semantic Kernel hosts: `STAF.LLMEval.SemanticKernel`.

See [PACKAGES.md](PACKAGES.md) and [MIGRATION-v3.md](MIGRATION-v3.md).

## What not to do

- Don’t call live cloud providers from every PR without caching or feature flags — flaky and costly.
- Don’t parse assertion exception strings in tests; assert on score / pass / suite rate.
- Don’t block the classic `EvaluateAsync` path when adding fluent helpers.
