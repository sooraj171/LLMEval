# STAF.LLMEval Release Roadmap

Living roadmap for release-aligned phases. Agents: when the user says **implement phase N**, implement **only** that phase’s scope, then update this file’s status and [`.cursor/rules/llmeval-releases.mdc`](.cursor/rules/llmeval-releases.mdc).

| Phase | Version | Name | Status |
|-------|---------|------|--------|
| — | v1.5.0 | Baseline (Direct / LLM-as-judge / Grounding + OpenAI/Gemini/Ollama) | Shipped |
| 1 | v2.0.0 | Adoption | **Done** |
| 2 | v2.1.0 | Evaluation Engine & Datasets | **Done** |
| 3 | v2.2.0 | Test Framework & CI Reporting | **Done** |
| 4 | v3.0.0 | Architecture & Ecosystem | **Done** |
| 5 | v3.1.0 | Community & Polish | **Done** |

---

## Phase 1 — v2.0.0 Adoption (**Done**)

**Shipped in:** package version `2.0.0` (feature); patch `2.0.1` (NuGet metadata/tags). Tag when publishing: `v2.0.1`

**Goal:** A .NET developer can evaluate LLM output in an xUnit test in under 5 minutes on .NET 8+, with a CI-friendly report—without rewriting existing `EvaluateAsync` callers.

### In scope

1. **Multi-TFM + CI**
   - Target `net8.0;net9.0;net10.0` in `LLMEval/LLMEval.csproj`
   - Fix `.github/workflows/nugetPublish.yml` for correct SDK/TFMs
   - Add `.github/workflows/ci.yml` (restore → build → test on PR/push)
   - SourceLink, XML docs, symbols, accurate `RepositoryUrl`
   - Bump package version to **2.0.0**; keep `PackageId` = `STAF.LLMEval`

2. **Fluent API + Options + DI**
   - Fluent builder: `Eval.Direct()`, `Eval.Judge()`, `Eval.Grounding()`
   - `LLMEvalOptions` + `services.AddLLMEval(...)`
   - Bridge options → existing `Configuration` dictionary
   - Prefer `IHttpClientFactory` in DI path; keep existing ctors for BC
   - Keep `IEvaluationService.EvaluateAsync` / `EvaluationRequest` as stable path

3. **Assertions**
   - `ShouldPass()`, `ShouldScoreAbove(n)`, `ShouldBeGrounded()`
   - Throw `LLMEvalAssertionException` with score, details, unsupported claims
   - Ship in main package (no separate Assertions NuGet yet)

4. **Suite + reporting**
   - `EvaluationSuite` from JSON/JSONL
   - Emit `report.json` + `report.html` (prompt, expected, actual, score, pass, details)
   - Aggregate pass rate; `MeetsPassRate` for CI
   - Bounded parallelism for **suite** case execution (grounding claims stay sequential for correct mapping)

5. **Azure OpenAI provider**
   - `ProviderType.AzureOpenAI` + provider + factory wiring
   - Unit tests with mocked HTTP

6. **Docs & samples**
   - Root `README.md` leading with zero-key DirectEvaluation
   - Fix package `LLMEval/Readme.md` (`IsReferenceDoc`, etc.)
   - `samples/MinimalXunit`
   - `CHANGELOG.md` with migration notes

7. **Hygiene**
   - Obsolete unused GloVe path; TF-IDF remains default semantic
   - Map `ModelName` → `Configuration["Model"]` when set

### Out of scope (do not implement in Phase 1)

- Core/Abstractions multi-project split
- Separate NuGet packages (Assertions, Reporting, MSTest, xUnit, NUnit, …)
- Toxicity / faithfulness / schema plugin metrics
- Claude, Bedrock, Vertex, Groq, Cohere, Mistral
- Semantic Kernel / Aspire / Playwright / MCP
- Excel datasets, trend dashboards, BenchmarkDotNet
- Community blogs / extra sample repos

### Definition of Done

- [x] Builds and tests pass on net8 / net9 / net10
- [x] PR CI workflow added (`.github/workflows/ci.yml`)
- [x] Existing `EvaluateAsync` usage still compiles
- [x] Fluent + assertions usable from MinimalXunit sample
- [x] Suite can produce HTML + JSON report
- [x] Azure OpenAI documented and unit-tested
- [x] CHANGELOG + this ROADMAP + rule Status updated for Phase 2 next

### Key files

- `LLMEval/LLMEval.csproj`, `LLMEval/IEvaluationService.cs`, `LLMEval/EvaluationRequest.cs`
- `LLMEval/Eval.cs`, `LLMEval/EvaluationSuite.cs`, `LLMEval/AzureOpenAIProvider.cs`
- `.github/workflows/`
- `samples/MinimalXunit/`
- `CHANGELOG.md`, `README.md`, this file

---

## Phase 2 — v2.1.0 Evaluation Engine & Datasets (**Done**)

**Shipped in:** package version `2.1.0`. Tag when publishing: `v2.1.0`

### In scope

- Plugin-style metrics: exact, semantic similarity (clarify), JSON/schema validation, relevance; expand groundedness/hallucination story
- CSV (+ keep JSON/JSONL) dataset loading
- Golden datasets + baseline comparison (compare suite run to previous baseline JSON)
- Markdown + CSV report writers
- Token/cost fields on results when providers expose usage (best-effort)

### Out of scope

- Full toxicity/bias model suite unless lightweight heuristic
- Package split
- Non-OpenAI cloud providers beyond Azure (those are Phase 4)

### Definition of Done

- [x] Custom or built-in metrics registerable without forking core
- [x] Baseline diff in CI sample
- [x] Docs + CHANGELOG; ROADMAP marks Phase 3 next

---

## Phase 3 — v2.2.0 Test Framework & CI Reporting (**Done**)

**Shipped in:** package version `2.2.0`. Tag when publishing: `v2.2.0`

**Status:** **Done**

### In scope

- Optional `STAF.LLMEval.MSTest` / `.xUnit` / `.NUnit` packages **only if** main-package asserts are insufficient
- GitHub Actions + Azure DevOps sample pipelines with pass-rate failure thresholds
- Publish report artifacts from CI samples
- Richer assertion messages / traits for filtering eval tests

### Out of scope

- Aspire / Playwright / MCP (Phase 4)
- Architecture split (Phase 4)

### Definition of Done

- [x] At least one official CI template (GitHub Actions) with threshold fail
- [x] Azure DevOps sample pipeline + report artifact publish
- [x] Richer asserts (`because`, metric/grounding/usage, `ShouldMeetPassRate`) + `EvalTraits` / case tags
- [x] Docs + CHANGELOG; ROADMAP marks Phase 4 next
- [x] Separate framework packages **skipped** — main-package asserts remain sufficient

### Key files

- `LLMEval/EvaluationResultAssertions.cs`, `LLMEval/EvalTraits.cs`, `LLMEval/ReportPaths.cs`, `LLMEval/EvaluationSuite.cs`
- `samples/ci/`, `samples/MinimalXunit/`, `.github/workflows/ci.yml`
- `CHANGELOG.md`, `README.md`, this file

---

## Phase 4 — v3.0.0 Architecture & Ecosystem (**Done**)

**Shipped in:** package version `3.0.0`. Tag when publishing: `v3.0.0`

**Status:** **Done**

### In scope

- Incremental Core/Abstractions split **with BC shims** in `STAF.LLMEval` meta-package
- Providers: Claude, plus 1–2 high-demand others (prioritize by issues/stars)
- Semantic Kernel integration package
- Consider Aspire / ASP.NET Core helpers if demand exists

### Out of scope

- Every provider in the original master list in one release
- Playwright/MCP unless already requested by users

### Definition of Done

- [x] Multi-package strategy documented; main package still one-line install
- [x] Migration guide; ROADMAP marks Phase 5 next
- [x] Claude + Groq + Mistral providers
- [x] `STAF.LLMEval.SemanticKernel` package
- [x] ASP.NET `AddLLMEval(IConfiguration)` (Aspire deferred as separate package)

### Key files

- `LLMEval.Abstractions/`, `LLMEval.Core/`, `LLMEval/` (meta + TypeForwards)
- `LLMEval.SemanticKernel/`
- `docs/PACKAGES.md`, `docs/MIGRATION-v3.md`
- `CHANGELOG.md`, `README.md`, this file

---

## Phase 5 — v3.1.0 Community & Polish (**Done**)

**Shipped in:** package version `3.1.0`. Tag when publishing: `v3.1.0`

**Status:** **Done**

### In scope

- Contribution guide, GitHub Discussions prompt, NuGet metadata optimization
- BenchmarkDotNet for hot paths
- Extra sample repositories / blog outline (content may live outside repo)
- Performance guide, best practices docs

### Out of scope

- New providers / Aspire / Playwright / MCP (deferred unless demanded)

### Definition of Done

- [x] CONTRIBUTING.md + benchmarks smoke run in CI
- [x] ROADMAP reflects maintenance mode

### Key files

- `CONTRIBUTING.md`, `docs/BEST-PRACTICES.md`, `docs/PERFORMANCE.md`, `docs/BLOG-OUTLINE.md`
- `benchmarks/LLMEval.Benchmarks/`, `.github/workflows/ci.yml`
- `CHANGELOG.md`, `README.md`, this file

---

## Maintenance

Phases 1–5 are **complete**. Further work is demand-driven (new providers, Aspire/Playwright/MCP, companion samples). Open a GitHub Discussion or Issue before large scope.

---

## Standing rules (all phases)

- **Backward compatibility** unless explicitly approved for that release.
- **Async-first**, nullable enabled, XML docs on public APIs.
- **Deliverables per phase:** code, unit tests, sample/docs touch, CHANGELOG, migration notes, ROADMAP + rule status update.
- Prefer **one package** until Phase 4; Phase 4+ may split packages with `STAF.LLMEval` meta remaining the default install.
