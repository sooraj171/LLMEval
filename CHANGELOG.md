# Changelog

All notable changes to **STAF.LLMEval** (NuGet: [STAF.LLMEval](https://www.nuget.org/packages/STAF.LLMEval)) are documented here.

STAF.LLMEval is a .NET LLM evaluation / AI testing framework for AI response evaluation, LLM-as-judge scoring, RAG grounding, and hallucination detection.

## [2.1.0] - 2026-08-06

### Summary

Evaluation engine & datasets: pluggable metrics, CSV loading, golden baseline comparison, Markdown/CSV reports, and best-effort token/cost usage—while keeping classic `EvaluateAsync` compatibility.

### Added

- **Plugin metrics:** `IEvaluationMetric`, `MetricRegistry`, `MetricContext` / `MetricResult`
  - Built-ins: `exact`, `keyword`, `semantic` (TF-IDF — clarified in details), `json`, `schema`, `relevance`, `grounded-heuristic`
  - Custom metrics via `MetricRegistry.Register` or `services.AddLLMEvalMetric<T>()` + `AddLLMEval(..., configureMetrics:)`
- Fluent Direct helpers: `.Json()`, `.Schema()`, `.Relevance()`, `.GroundedHeuristic()`, `.WithMetric(...)`
- **CSV** dataset loading in `EvaluationSuite` (JSON / JSONL unchanged)
- **Baseline comparison:** `BaselineComparer.Compare` / `CompareToBaselineFileAsync` / `WriteDiffReportAsync`
- Report writers: `report.md` + `report.csv` (still writes `report.json` + `report.html`)
- **TokenUsage** on `EvaluationResult` / suite results when providers expose usage; optional `InputCostPer1M` / `OutputCostPer1M` for `EstimatedCostUsd`
- Grounding fields: `GroundednessScore`, `HallucinationRate`
- `EvaluationRequest.Schema` for schema metric
- MinimalXunit: CSV sample + baseline regression test

### Changed

- Package version **2.1.0**
- DirectEvaluation matching routes through `MetricRegistry` (behavior of exact/keyword/semantic preserved)
- Suite reports include metric name and optional aggregate usage

### Migration from 2.0.x

- Existing `EvaluationRequest` + `EvaluateAsync` / `Eval.*` code continues to work
- `WriteReportsAsync` now also emits `report.md` and `report.csv` (additive)
- Unknown `MatchingType` values now fail clearly instead of silently falling back to exact — register a custom metric or use a built-in name
- Semantic Direct matching remains **TF-IDF** (not embeddings); details text now says so explicitly

## [2.0.1] - 2026-08-06

### Changed

- NuGet **title**, **description**, **PackageTags**, and **PackageReleaseNotes** expanded for discoverability (LLM evaluation, AI testing, RAG, hallucination, ChatGPT/GPT, etc.)
- README / changelog wording aligned for NuGet.org, Google, and AI search
- **No API or behavior changes** vs 2.0.0

## [2.0.0] - 2026-08-06

### Summary (NuGet release notes)

Adoption release: multi-TFM (.NET 8/9/10), fluent Eval API, assertions, DI/Options, evaluation suites with HTML/JSON reports, and Azure OpenAI—while keeping classic `EvaluateAsync` compatibility.

### Added

- Multi-target frameworks: `net8.0`, `net9.0`, `net10.0`
- Fluent API: `Eval.Direct()`, `Eval.Judge()`, `Eval.Grounding()`
- Assertions: `ShouldPass()`, `ShouldScoreAbove()`, `ShouldBeGrounded()` (`LLMEvalAssertionException`)
- Options + DI: `LLMEvalOptions`, `services.AddLLMEval(...)`
- Suite runner: `EvaluationSuite` (JSON / JSONL) with `report.json` + `report.html`
- Azure OpenAI provider (`ProviderType.AzureOpenAI`)
- PR CI workflow (build + test); publish workflow updated for multi-TFM + symbols
- Root README, MinimalXunit sample, ROADMAP

### Changed

- Package version **2.0.0**; SourceLink, XML docs, symbol packages enabled
- `ModelName` is applied to `Configuration["Model"]` when Model is unset
- Grounding judge calls remain sequential (correct claim ordering); suite runs use bounded parallelism

### Obsolete

- `GloveModel` and `SemanticSimilarityEvaluator` — not wired into `AdvancedEvaluationService`; use `MatchingType = "semantic"` (TF-IDF)

### Migration from 1.x

- Existing `EvaluationRequest` + `EvaluateAsync` code continues to work
- Prefer `Eval.*` and assertions for new tests
- If you only targeted `net10.0`, you can now also consume from net8/net9 projects
- Fix any docs/code that used `IsReferenceDocument` — the property is `IsReferenceDoc`

## [1.5.0] - prior

- DirectEvaluation, LLMAsJudge, GroundedAnswerCheck
- Providers: OpenAI, Gemini, Ollama
