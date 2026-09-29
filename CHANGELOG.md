# Changelog

All notable changes to **STAF.LLMEval** (NuGet: [STAF.LLMEval](https://www.nuget.org/packages/STAF.LLMEval)) are documented here.

STAF.LLMEval is a .NET LLM evaluation / AI testing framework for AI response evaluation, LLM-as-judge scoring, RAG grounding, and hallucination detection.

## [3.3.0] - 2026-09-29

### Summary

The GitHub and NuGet READMEs now open with why string-equality tests fail on LLM output. Embedding similarity is an explicit opt-in that fails the build when no provider is configured. TF-IDF `semantic` scores are unchanged. Packages ship a NuGet icon. Public sample [sooraj171/LLMEval.Sample](https://github.com/sooraj171/LLMEval.Sample) references `STAF.LLMEval` **2.2.0** (the latest NuGet release that restores; published 3.x packages depend on `STAF.LLMEval.Core`, which is not on nuget.org yet).

### Added

- `Eval.Direct().SemanticEmbedding(...)` and `MatchingType = semantic-embedding` (same metric as `embedding-semantic`)
- `EmbeddingProviderType` and `WithProvider(EmbeddingProviderType)` for OpenAI and Azure OpenAI embeddings
- `LLMEvalConfigurationException` when `semantic-embedding` / `embedding-semantic` has no `IEmbeddingProvider` and no API key
- `AddLLMEval` uses a registered `IEmbeddingProvider` for that metric
- NuGet `PackageIcon` on meta, Core, Abstractions, and SemanticKernel

### Changed

- A missing embedding provider **throws** instead of returning score 0. TF-IDF `MatchingType = semantic` is unchanged and still needs no key
- READMEs lead with the problem statement, then install

### Migration from 3.2.1

- Call sites that used `embedding-semantic` without an API key and asserted a failed score of 0 now get `LLMEvalConfigurationException`. Configure `WithProvider` / `WithApiKey`, or `services.AddSingleton<IEmbeddingProvider, T>()`
- `semantic` thresholds and baselines do not need to move
- `Eval.Direct().EmbeddingSemantic(...)` still works

## [3.2.1] - 2026-09-28

### Summary

Dependency and docs refresh: latest Microsoft.Extensions / SourceLink / Semantic Kernel / test SDK packages, and README files brought in line with 3.2 embeddings, run history, and samples. No public API changes vs 3.2.0.

### Changed

- Package version **3.2.1** (meta, Core, Abstractions, SemanticKernel)
- **Dependencies:** `Microsoft.Extensions.*` 10.0.12, `Microsoft.SourceLink.GitHub` 10.0.401, `Microsoft.SemanticKernel.Abstractions` 1.80.1
- **Dev/test:** `Microsoft.NET.Test.Sdk` 18.10.1, `xunit.runner.visualstudio` 3.1.5 (still xUnit 2.9.3 — runner 4.x is xUnit v3), `coverlet.collector` 10.1.0, `BenchmarkDotNet` 0.15.8
- NuGet and repo READMEs: embeddings metric, opt-in run history, QuickStart / OpenAIJudge samples

### Migration from 3.2.0

- No API or behavior changes required; bump package version only
- Apps that pin older `Microsoft.Extensions.*` 8.x will unify to 10.0.12 (still supports `net8.0`)
- `STAF.LLMEval.SemanticKernel` now depends on Semantic Kernel **1.80.1**

## [3.2.0] - 2026-09-04

### Summary

Adoption quality: README/discoverability, contributor & CI hygiene, embeddings-based semantic similarity (opt-in metric), opt-in suite run history with an HTML pass-rate trend, and copy-paste samples — no breaking API changes.

### Added

- **Embeddings metric:** `EmbeddingSemanticMetric` (`MatchingType = embedding-semantic`), fluent `Eval.Direct().EmbeddingSemantic(...)`, pluggable `IEmbeddingProvider` with `OpenAIEmbeddingProvider` (OpenAI + Azure OpenAI). TF-IDF `semantic` remains the zero-dependency default.
- **Opt-in run history:** `LLMEvalOptions.EnableRunHistory` appends JSONL summaries and draws a pass-rate sparkline on `report.html`. Default behavior unchanged.
- **Samples:** `samples/QuickStart` (no API keys) and `samples/OpenAIJudge` (skipped unless `OPENAI_API_KEY` is set)
- **Contributor hygiene:** issue templates (bug / feature), PR template, CONTRIBUTING coverage for metrics and providers; CI packs nupkgs on PR and runs QuickStart

### Changed

- Package version **3.2.0** (meta, Core, Abstractions, SemanticKernel)
- Root README leads with the CI/non-determinism problem, dynamic NuGet/build/license/TFM badges, and a single copy-paste Exact() quick start
- `EvaluateDirectlyAsync` forwards `CancellationToken` and request `Endpoint` / `ProviderType` into `MetricContext`

### Migration from 3.1.x

- No API or behavior changes required; bump package version only
- To use embeddings: `Eval.Direct().EmbeddingSemantic(actual, expected).WithApiKey(...).WithModel("text-embedding-3-small")` or `registry.Register(new EmbeddingSemanticMetric(myProvider))`
- To enable history: `new EvaluationSuite(service, new LLMEvalOptions { EnableRunHistory = true })`

## [3.1.0] - 2026-08-12

### Summary

Community & polish: contribution guide, Discussions prompt, best practices and performance docs, blog/sample outline, BenchmarkDotNet hot-path suite with CI smoke, and refreshed NuGet metadata—no breaking API changes.

### Added

- **[`CONTRIBUTING.md`](CONTRIBUTING.md)** — setup, PR checklist, GitHub Discussions guidance
- **Docs:** [`docs/BEST-PRACTICES.md`](docs/BEST-PRACTICES.md), [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md), [`docs/BLOG-OUTLINE.md`](docs/BLOG-OUTLINE.md)
- **`benchmarks/LLMEval.Benchmarks`** — BenchmarkDotNet suite (exact / keyword / semantic metrics, JSON parse, HtmlResult)
- CI step: short in-process benchmark smoke on PR/push

### Changed

- Package version **3.1.0** (meta, Core, Abstractions, SemanticKernel)
- NuGet description / tags / release notes expanded for discoverability

### Migration from 3.0.x

- No API or behavior changes required; bump package version only

## [3.0.0] - 2026-08-06

### Summary

Architecture & ecosystem: incremental Core/Abstractions package split with BC type-forwards in the `STAF.LLMEval` meta-package, Claude/Groq/Mistral providers, optional Semantic Kernel integration, and ASP.NET configuration helpers—while keeping one-line install and classic `EvaluateAsync` source compatibility.

### Added

- **Packages:** `STAF.LLMEval.Abstractions`, `STAF.LLMEval.Core`; `STAF.LLMEval` remains the recommended meta-package (`TypeForwardedTo` shims)
- **Providers:** `ProviderType.Claude` (Anthropic Messages API), `Groq`, `Mistral` (OpenAI-compatible)
- **`STAF.LLMEval.SemanticKernel`:** `SemanticKernelChatProvider`, `SemanticKernelProviderFactory`, `AddLLMEvalSemanticKernel()`
- **ASP.NET helper:** `services.AddLLMEval(IConfiguration)` binds section `LLMEval`
- Docs: [`docs/PACKAGES.md`](docs/PACKAGES.md), [`docs/MIGRATION-v3.md`](docs/MIGRATION-v3.md)

### Changed

- Package version **3.0.0**
- Public types live in `LLMEval.Abstractions` / `LLMEval.Core` assemblies (namespaces remain `LLMEval`)

### Not added (by design)

- Full Aspire hosting package / Playwright / MCP (defer until demand; config binding covers host apps)
- Remaining cloud providers from the master list (Bedrock, Vertex, Cohere, …)

### Migration from 2.2.x

- Rebuild after upgrade (assembly split). Source APIs unchanged for typical callers.
- See [`docs/MIGRATION-v3.md`](docs/MIGRATION-v3.md)
- Exhaustive `switch` on `ProviderType` must handle Claude / Groq / Mistral

## [2.2.0] - 2026-08-06

### Summary

Test framework & CI reporting: richer assertion messages, suite pass-rate asserts, eval traits / case tags for filtering, and official GitHub Actions + Azure DevOps templates that fail on pass-rate and publish report artifacts—without separate MSTest/xUnit/NUnit packages.

### Added

- **Richer assertions:** multiline failure text with metric, groundedness, hallucination rate, partial support, usage/cost; optional `because:` on `ShouldPass` / `ShouldScoreAbove` / `ShouldBeGrounded`
- **`ShouldMeetPassRate(minimum)`** on `SuiteRunResult` (lists failed case ids); `LLMEvalAssertionException.SuiteResult`
- **`EvalTraits`** — `Category` / `Kind` / `Tag` constants for xUnit `[Trait]`, MSTest `[TestCategory]`, NUnit `[Category]`
- **Suite case tags:** `SuiteCase.Tags` + `FilterByTags` (JSON array / CSV `tags` column with `;` `|` `,`)
- **`ReportPaths.ResolveReportDirectory`** — honors `LLMEVAL_REPORT_DIR` for CI artifact folders
- **CI templates:** `samples/ci/github-actions-llmeval.yml`, `samples/ci/azure-pipelines-llmeval.yml` (pass-rate via test asserts + upload/publish reports)
- Main `.github/workflows/ci.yml` runs MinimalXunit with `--filter Category=LLMEval` and uploads `artifacts/llmeval`

### Changed

- Package version **2.2.0**
- MinimalXunit sample uses traits, `ShouldMeetPassRate`, tagged cases, and `LLMEVAL_REPORT_DIR`

### Not added (by design)

- Separate `STAF.LLMEval.MSTest` / `.xUnit` / `.NUnit` packages — main-package asserts are framework-agnostic and sufficient for this release

### Migration from 2.1.x

- Existing `EvaluateAsync` / `Eval.*` / `ShouldPass()` call sites continue to work (`because` is optional)
- Assertion messages are multiline and include more fields (update any brittle string asserts on exception text)
- Prefer `report.ShouldMeetPassRate(0.9)` over raw `Assert.True(report.MeetsPassRate(0.9))` for clearer CI failures

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

### Migration from 1.x

- Existing `EvaluateAsync` / `EvaluationRequest` call sites continue to work
- Prefer `Eval.*` + assertions for new tests; suite reports are additive
