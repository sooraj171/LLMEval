# STAF.LLMEval

**STAF.LLMEval** is a .NET **LLM evaluation** and **AI testing** library for validating **generative AI** responses (ChatGPT, GPT, Gemini, Ollama, Azure OpenAI, Claude, Groq, Mistral) in unit tests and CI pipelines.

Score outputs with **pluggable metrics** (exact, keyword, TF-IDF semantic similarity, JSON/schema, relevance, heuristic grounding), **LLM-as-judge**, and **RAG grounding / hallucination detection**. Includes a fluent `Eval` API, test assertions, Options/DI, JSON/JSONL/CSV evaluation suites, golden baseline comparison, and HTML/JSON/Markdown/CSV reports.

**Package:** [STAF.LLMEval](https://www.nuget.org/packages/STAF.LLMEval) · **Version:** 3.1.0 · **Targets:** `net8.0`, `net9.0`, `net10.0` · **License:** MIT

## Release notes — 3.1.0

- **Community:** CONTRIBUTING.md, Discussions guidance, best practices / performance docs, blog outline
- **Benchmarks:** `benchmarks/LLMEval.Benchmarks` + CI smoke
- NuGet metadata/tags refreshed — **no API breaks** vs 3.0.0

## Release notes — 3.0.0

- **Multi-package:** `STAF.LLMEval` meta → `Core` + `Abstractions` (type forwards); still one-line install
- **Providers:** Anthropic **Claude**, **Groq**, **Mistral** (+ OpenAI / Azure / Gemini / Ollama)
- **Optional:** `STAF.LLMEval.SemanticKernel` for Semantic Kernel chat completion
- **ASP.NET / Aspire-friendly:** `services.AddLLMEval(configuration)` binds section `LLMEval`
- Migration: [docs/MIGRATION-v3.md](https://github.com/sooraj171/LLMEval/blob/main/docs/MIGRATION-v3.md) · packages: [docs/PACKAGES.md](https://github.com/sooraj171/LLMEval/blob/main/docs/PACKAGES.md)

## Release notes — 2.2.0

- **Richer asserts:** metric / grounding / usage in failure messages; optional `because:`; `ShouldMeetPassRate` for suite CI gates
- **`EvalTraits`** + suite case **tags** / `FilterByTags` for CI filtering
- **`LLMEVAL_REPORT_DIR`** / `ReportPaths` for artifact folders
- **CI templates:** GitHub Actions + Azure DevOps (`samples/ci`) with pass-rate fail + report upload
- Framework-specific NuGet packages not required — asserts stay in the main package

## Release notes — 2.1.0

- **Plugin metrics:** `IEvaluationMetric` / `MetricRegistry` (exact, keyword, semantic TF-IDF, json, schema, relevance, grounded-heuristic + custom)
- **Datasets:** CSV (+ JSON/JSONL); golden **baseline comparison** for CI
- **Reports:** Markdown + CSV in addition to HTML/JSON
- **Usage:** best-effort `TokenUsage` / cost when providers return usage
- **Grounding:** `GroundednessScore`, `HallucinationRate`
- **Backward compatible** with `EvaluationRequest` / `EvaluateAsync`

Full changelog: https://github.com/sooraj171/LLMEval/blob/main/CHANGELOG.md

## Installation

```bash
dotnet add package STAF.LLMEval
```

## Quick start (no API key)

```csharp
using LLMEval;

var result = await Eval.Direct()
    .Exact(actual: "Paris", expected: "Paris")
    .WithThreshold(1.0)
    .EvaluateAsync();

result.ShouldPass();
result.ShouldScoreAbove(0.9);
```

Other Direct matchers:

```csharp
await Eval.Direct().Keyword(actual, expected).WithThreshold(0.5).EvaluateAsync();
await Eval.Direct().Semantic(actual, expected).WithThreshold(0.3).EvaluateAsync(); // TF-IDF (not embeddings)
await Eval.Direct().Json("""{"ok":true}""").EvaluateAsync();
await Eval.Direct().Schema(actualJson, jsonSchema).EvaluateAsync();
await Eval.Direct().Relevance(question, actual).WithThreshold(0.2).EvaluateAsync();
await Eval.Direct().GroundedHeuristic(actual, referenceDoc).WithThreshold(0.5).EvaluateAsync();
```

## Assertions

```csharp
result.ShouldPass(because: "exact capital");
result.ShouldScoreAbove(0.8);
result.ShouldBeGrounded(); // fails if RiskLevel is High or unsupported statements exist
report.ShouldMeetPassRate(0.9, because: "CI gate");
```

Failures throw `LLMEvalAssertionException` with score, metric, grounding, usage, and optional `because` context.
`SuiteResult` is set when a suite pass-rate assert fails.

### Filtering eval tests (traits)

```csharp
[Fact]
[Trait(EvalTraits.Category, EvalTraits.LLMEval)]
[Trait(EvalTraits.Kind, EvalTraits.Direct)]
[Trait(EvalTraits.Tag, EvalTraits.Smoke)]
public async Task ExactMatch_ShouldPass() { ... }
```

```bash
dotnet test --filter "Category=LLMEval"
dotnet test --filter "Category=LLMEval&Tag=Smoke"
```

Same string constants work with MSTest `[TestCategory(EvalTraits.LLMEval)]` and NUnit `[Category(EvalTraits.LLMEval)]`.

## Fluent judge & grounding

```csharp
using LLMEval;

// LLM-as-judge
var judgeResult = await Eval.Judge()
    .WithQuestion("What is the capital of France?")
    .WithResponse("Paris")
    .WithExpected("Paris")
    .WithProvider(ProviderType.OpenAI)
    .WithApiKey(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
    .WithModel("gpt-4o-mini")
    .WithTemperature(0)
    .WithThreshold(0.8)
    .EvaluateAsync();

judgeResult.ShouldPass();

// Grounding / hallucination check
var grounding = await Eval.Grounding()
    .WithQuestion("Summarize the document.")
    .WithResponse("The report states X. It also says Y.")
    .WithExpected("Full reference document text...")
    .WithReferenceDocuments("Doc A...", "Doc B...") // optional; overrides expected as reference
    .WithProvider(ProviderType.Ollama)
    .WithEndpoint("http://localhost:11434")
    .WithModel("llama3.2")
    .WithThreshold(0.8)
    .EvaluateAsync();

grounding.ShouldBeGrounded();
// Also: grounding.GroundednessScore, grounding.HallucinationRate, grounding.Usage
```

Inject an existing service with `.Using(evaluationService)` or apply shared settings with `.WithOptions(options)`.

## Configuration (Options + DI)

```csharp
using Microsoft.Extensions.DependencyInjection;
using LLMEval;

services.AddLLMEvalMetric<MyCustomMetric>(); // optional
services.AddLLMEval(o =>
{
    o.DefaultProvider = ProviderType.OpenAI;
    o.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
    o.Model = "gpt-4o-mini";
    o.Temperature = "0";
    o.DefaultPassThreshold = 0.8;
    o.MaxDegreeOfParallelism = 4; // suite case parallelism
}, configureMetrics: registry =>
{
    // registry.Register(new MyCustomMetric());
});

var eval = serviceProvider.GetRequiredService<IEvaluationService>();
```

Store secrets in environment variables or user secrets — never commit API keys.

### Provider configuration keys

Still supported on `EvaluationRequest.Configuration`:

| Key | Purpose |
|-----|---------|
| `ApiKey` | Cloud provider API key |
| `Model` | Model name or Azure deployment name |
| `Temperature` | Sampling temperature (prefer `"0"` in CI) |
| `ApiVersion` | Azure OpenAI API version (optional) |
| `InputCostPer1M` / `OutputCostPer1M` | Optional USD per 1M tokens for `EstimatedCostUsd` |

`EvaluationRequest.ModelName` is copied into `Configuration["Model"]` when Model is not set.

## Core types

### `EvaluationRequest`

- `Question`, `AiResponse`, `GoldenOutput`, optional `Schema`
- `ProviderType`: `Ollama`, `OpenAI`, `Gemini`, `AzureOpenAI`, `Claude`, `Groq`, `Mistral`
- `Endpoint`, `Configuration`, `PassThreshold`, `ModelName`
- `MatchingType`: `exact`, `keyword`, `semantic` (TF-IDF), `json`, `schema`, `relevance`, `grounded-heuristic`, or any registered custom name
- `EvaluationType`: `DirectEvaluation`, `LLMAsJudge`, `GroundedAnswerCheck`
- `IsReferenceDoc` — treat `GoldenOutput` as a reference document for LLM-as-judge
- `ReferenceDocuments` — optional multi-doc list for grounding (overrides `GoldenOutput` when set)

### `EvaluationResult`

- `Score`, `IsPassed`, `Details`, `Confidence`, `MetricName`
- Grounding: `UnsupportedStatements`, `PartiallySupportedStatements`, `RiskLevel`, `GroundednessScore`, `HallucinationRate`
- `Usage` — best-effort `TokenUsage` when the provider response includes usage metadata

## Classic usage (fully supported)

```csharp
using LLMEval;

IAiProviderFactory providerFactory = new AiProviderFactory();
IEvaluationService evalService = new AdvancedEvaluationService(providerFactory);

var request = new EvaluationRequest
{
    Question = "What is the capital of France?",
    AiResponse = "Paris, France",
    GoldenOutput = "Paris",
    ProviderType = ProviderType.OpenAI,
    Endpoint = "https://api.openai.com/v1/chat/completions",
    Configuration = new Dictionary<string, string>
    {
        ["ApiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
        ["Model"] = "gpt-4o-mini",
        ["Temperature"] = "0"
    },
    PassThreshold = 0.8,
    EvaluationType = EvaluationType.LLMAsJudge,
    IsReferenceDoc = false
};

EvaluationResult result = await evalService.EvaluateAsync(request);
result.ShouldPass();
```

### Azure OpenAI

```csharp
var request = new EvaluationRequest
{
    Question = "What is the capital of France?",
    AiResponse = "Paris",
    GoldenOutput = "Paris",
    ProviderType = ProviderType.AzureOpenAI,
    Endpoint = "https://YOUR_RESOURCE.openai.azure.com",
    Configuration = new Dictionary<string, string>
    {
        ["ApiKey"] = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!,
        ["Model"] = "your-deployment-name",
        ["Temperature"] = "0"
        // optional: ["ApiVersion"] = "2024-02-15-preview"
    },
    PassThreshold = 0.8,
    EvaluationType = EvaluationType.LLMAsJudge
};
```

### GroundedAnswerCheck

```csharp
var request = new EvaluationRequest
{
    Question = "Summarize the document.",
    AiResponse = "The report states X. It also says Y and Z.",
    GoldenOutput = "Full reference document text...",
    // or: ReferenceDocuments = new[] { "Doc1...", "Doc2..." },
    ProviderType = ProviderType.Ollama,
    Endpoint = "http://localhost:11434",
    Configuration = new Dictionary<string, string>
    {
        ["Model"] = "llama3.2",
        ["Temperature"] = "0"
    },
    PassThreshold = 0.8,
    EvaluationType = EvaluationType.GroundedAnswerCheck
};

var result = await evalService.EvaluateAsync(request);
result.ShouldBeGrounded();
```

## Suite + reports (JSON/HTML/Markdown/CSV)

```csharp
var cases = await EvaluationSuite.LoadAsync("cases.json"); // also .jsonl / .csv
var smoke = cases.FilterByTags("smoke"); // optional tags on cases
var suite = new EvaluationSuite(evalService);
var report = await suite.RunAsync(smoke);
var outDir = ReportPaths.ResolveReportDirectory("./artifacts"); // honors LLMEVAL_REPORT_DIR
await suite.WriteReportsAsync(report, outDir); // report.json + .html + .md + .csv
// report.html uses the STAF HtmlResult skin (same visual language as STAF.Playwright)

report.ShouldMeetPassRate(0.9, because: "CI pass-rate threshold");

var diff = await BaselineComparer.CompareToBaselineFileAsync(report, "baseline-report.json");
if (diff.HasRegressions)
    throw new Exception(diff.ToSummary());
```

Dataset fields: `id`, `question`, `actual`, `expected`, `evaluationType`, `matchingType`, `threshold`, optional `provider`, `endpoint`, `model`, `schema`, `isReferenceDoc`, `referenceDocuments`, `tags`.

Formats: JSON array, JSONL, CSV (header row; `tags` as `smoke;ci`), or `{ "cases": [ ... ] }`.

CI templates: see repo `samples/ci` (GitHub Actions + Azure DevOps) for pass-rate failure + report artifact publish.

## Providers

| Provider | Endpoint | Notes |
|----------|----------|--------|
| OpenAI | Optional (defaults to chat completions URL) | `ApiKey`, `Model` |
| Azure OpenAI | Resource root or full chat/completions URL | `Model` = deployment name; optional `ApiVersion` |
| Gemini | Required | `ApiKey` on URL/query as implemented by provider |
| Ollama | e.g. `http://localhost:11434` | Local; `Model` required |
| Claude | Optional (`https://api.anthropic.com/v1/messages`) | `ApiKey`, `Model`; optional `ApiVersion`, `MaxTokens` |
| Groq | Optional (`https://api.groq.com/openai/v1/chat/completions`) | OpenAI-compatible; `ApiKey`, `Model` |
| Mistral | Optional (`https://api.mistral.ai/v1/chat/completions`) | OpenAI-compatible; `ApiKey`, `Model` |

Optional: `STAF.LLMEval.SemanticKernel` — `AddLLMEvalSemanticKernel()` uses Kernel chat completion for judge/grounding.

ASP.NET / host config: `services.AddLLMEval(configuration)` binds the `LLMEval` section.

## Notes

- Prefer `Temperature=0` for deterministic judge / grounding runs in CI.
- Default semantic matching uses **TF-IDF** (not embeddings). `GloveModel` / `SemanticSimilarityEvaluator` are obsolete and not used by `AdvancedEvaluationService`.
- Register custom DirectEvaluation metrics with `MetricRegistry` without forking core.
- Repository: https://github.com/sooraj171/LLMEval
- Changelog / migration: see repo `CHANGELOG.md` and `docs/MIGRATION-v3.md` (classic `EvaluateAsync` API retained).
