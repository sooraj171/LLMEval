# STAF.LLMEval

[![NuGet](https://img.shields.io/badge/NuGet-v3.2.1-0B3D91?logo=nuget&logoColor=white)](https://www.nuget.org/packages/STAF.LLMEval)
[![NuGet Downloads](https://img.shields.io/nuget/dt/STAF.LLMEval.svg)](https://www.nuget.org/packages/STAF.LLMEval)
[![Build](https://github.com/sooraj171/LLMEval/actions/workflows/ci.yml/badge.svg)](https://github.com/sooraj171/LLMEval/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://www.nuget.org/packages/STAF.LLMEval)

LLM outputs are non-deterministic: the same prompt can come back with different wording, extra clauses, or subtle hallucinations. Exact-string asserts flake in CI, and ad-hoc manual review does not scale. **STAF.LLMEval** is a .NET evaluation library that treats GenAI quality like any other test gate — start with zero-cost exact/keyword/JSON checks, then add TF-IDF or embeddings similarity, LLM-as-judge, and RAG grounding when you need them.

## Quick start

No API keys. Copy into an xUnit test (or run `dotnet test samples/QuickStart/QuickStart.csproj`).

```bash
dotnet add package STAF.LLMEval
```

```csharp
using LLMEval;

string aiResponse = "Paris"; // your app's LLM output

var result = await Eval.Direct()
    .Exact(actual: aiResponse, expected: "Paris")
    .WithThreshold(1.0)
    .EvaluateAsync();

result.ShouldPass();
```

## What you can evaluate

| Mode | Fluent entry | Use when |
|------|----------------|----------|
| **Direct** (`exact` / `keyword` / `semantic` TF-IDF / `embedding-semantic` / `json` / `schema` / `relevance` / `grounded-heuristic`) | `Eval.Direct()` | Golden-answer & structure checks. TF-IDF needs no keys; embeddings need an OpenAI/Azure embeddings endpoint |
| **LLM-as-judge** | `Eval.Judge()` | Semantic quality scoring via a chat provider |
| **GroundedAnswerCheck** | `Eval.Grounding()` | RAG hallucination checks — each claim vs reference docs |

## Direct matchers

`Eval.Direct()` needs no provider. TF-IDF `Semantic()` is the zero-dependency default for “are these similar?”; use `EmbeddingSemantic()` when paraphrases matter (requires an embeddings API key).

```csharp
await Eval.Direct().Exact("Paris", "Paris").EvaluateAsync();
await Eval.Direct().Keyword(actual, expected).WithThreshold(0.5).EvaluateAsync();
await Eval.Direct().Semantic(actual, expected).WithThreshold(0.3).EvaluateAsync(); // TF-IDF
await Eval.Direct().Json("""{"ok":true}""").EvaluateAsync();
await Eval.Direct().Schema(actualJson, jsonSchema).EvaluateAsync();
await Eval.Direct().Relevance(question, actual).WithThreshold(0.2).EvaluateAsync();
await Eval.Direct().GroundedHeuristic(actual, reference).WithThreshold(0.5).EvaluateAsync();
```

### TF-IDF vs embeddings (same pair)

Lexical TF-IDF misses paraphrases. Embeddings catch meaning when you plug in OpenAI or Azure OpenAI (or any `IEmbeddingProvider`).

```csharp
const string actual = "A feline rested on the rug";
const string expected = "The cat sat on the mat";

var tfidf = await Eval.Direct()
    .Semantic(actual, expected)
    .WithThreshold(0.3)
    .EvaluateAsync();
// typically a low score — little token overlap after stop-word removal

var embeddings = await Eval.Direct()
    .EmbeddingSemantic(actual, expected)
    .WithApiKey(apiKey)                 // OpenAI or Azure OpenAI
    .WithModel("text-embedding-3-small")
    .WithThreshold(0.75)
    .EvaluateAsync();
// typically a high score — vectors are close even when wording differs
```

Azure OpenAI: `.WithProvider(ProviderType.AzureOpenAI).WithEndpoint("https://YOUR_RESOURCE.openai.azure.com")` and set `Model` to the **embeddings deployment** name. Inject a custom `IEmbeddingProvider` via `MetricRegistry.Register(new EmbeddingSemanticMetric(provider))` if you are not using OpenAI.

## Judge, grounding, assertions

```csharp
await Eval.Judge()
    .WithQuestion(q).WithResponse(actual).WithExpected(expected)
    .WithProvider(ProviderType.OpenAI)
    .WithApiKey(apiKey).WithModel("gpt-4o-mini")
    .WithThreshold(0.8)
    .EvaluateAsync();

await Eval.Grounding()
    .WithResponse(actual).WithExpected(referenceDoc)
    .WithProvider(ProviderType.Ollama)
    .WithEndpoint("http://localhost:11434")
    .WithModel("llama3.2")
    .EvaluateAsync();

result.ShouldPass(because: "exact capital");
result.ShouldScoreAbove(0.8);
result.ShouldBeGrounded();
```

Filter eval tests in CI: `[Trait(EvalTraits.Category, EvalTraits.LLMEval)]` then `dotnet test --filter "Category=LLMEval"`.

## DI / Options

```csharp
services.AddLLMEval(options =>
{
    options.DefaultProvider = ProviderType.AzureOpenAI;
    options.Endpoint = "https://YOUR_RESOURCE.openai.azure.com";
    options.ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;
    options.Model = "gpt-4o-mini";
    options.DefaultPassThreshold = 0.8;
});

services.AddLLMEval(builder.Configuration); // binds section "LLMEval"

var eval = sp.GetRequiredService<IEvaluationService>();
```

Classic API still works: `EvaluationRequest` + `AdvancedEvaluationService.EvaluateAsync`. Optional Semantic Kernel: `dotnet add package STAF.LLMEval.SemanticKernel`.

## Suite reports (CI)

```csharp
var cases = await EvaluationSuite.LoadAsync("cases.json"); // also .jsonl / .csv
var smoke = cases.FilterByTags("smoke");
var suite = new EvaluationSuite(evalService);
var report = await suite.RunAsync(smoke);
var outDir = ReportPaths.ResolveReportDirectory("artifacts");
await suite.WriteReportsAsync(report, outDir); // report.json + .html + .md + .csv

report.ShouldMeetPassRate(0.9, because: "CI pass-rate threshold");

var diff = await BaselineComparer.CompareToBaselineFileAsync(report, "baseline-report.json");
Assert.False(diff.HasRegressions, diff.ToSummary());
```

**Opt-in run history:** set `EnableRunHistory = true` on `LLMEvalOptions` to append each suite summary to a JSONL file and draw a pass-rate sparkline on the HTML report. Off by default.

```csharp
var suite = new EvaluationSuite(evalService, new LLMEvalOptions
{
    EnableRunHistory = true,
    TrendHistoryLength = 20
    // RunHistoryPath defaults to {outputDirectory}/history.jsonl
});
await suite.WriteReportsAsync(report, outDir);
```

Pipeline templates: [`samples/ci`](samples/ci) (GitHub Actions + Azure DevOps).

### Custom metrics

```csharp
services.AddLLMEvalMetric<MyMetric>();
services.AddLLMEval(configureMetrics: registry => registry.Register(new MyMetric()));

var registry = MetricRegistry.CreateDefault();
registry.Register(new MyMetric());
var service = new AdvancedEvaluationService(new AiProviderFactory(), new HttpClient(), null, registry);
```

## Providers

| Provider | Notes |
|----------|--------|
| **OpenAI** | Default chat completions (and embeddings) if `Endpoint` is empty |
| **Azure OpenAI** | Resource URL + deployment name in `Model` |
| **Gemini** | Requires endpoint + API key |
| **Ollama** | Local, e.g. `http://localhost:11434` |
| **Claude** | Anthropic Messages API |
| **Groq** | OpenAI-compatible (`api.groq.com`) |
| **Mistral** | OpenAI-compatible (`api.mistral.ai`) |

Optional cost estimate: `Configuration["InputCostPer1M"]` / `OutputCostPer1M` (USD per 1M tokens).

## Samples & docs

| Doc | Purpose |
|-----|---------|
| [`samples/QuickStart`](samples/QuickStart) | Zero-setup xUnit sample (`dotnet test`, no API keys) |
| [`samples/OpenAIJudge`](samples/OpenAIJudge) | LLM-as-judge sample (skipped unless `OPENAI_API_KEY` is set) |
| [`samples/MinimalXunit`](samples/MinimalXunit) | Traits, suites, baseline CI check |
| [`samples/ci`](samples/ci) | GitHub Actions + Azure DevOps templates |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Build, test, add a metric or provider, PR checklist |
| [`docs/BEST-PRACTICES.md`](docs/BEST-PRACTICES.md) | Eval / CI best practices |
| [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md) | Cost model, parallelism, benchmarks |
| [`docs/PACKAGES.md`](docs/PACKAGES.md) | Meta / Core / Abstractions / SK |
| [`docs/MIGRATION-v3.md`](docs/MIGRATION-v3.md) | Upgrade guide from 2.x → 3.x |
| [`benchmarks/LLMEval.Benchmarks`](benchmarks/LLMEval.Benchmarks) | BenchmarkDotNet hot-path suite |
| [`CHANGELOG.md`](CHANGELOG.md) | Release notes |
| [`ROADMAP.md`](ROADMAP.md) | Release phases |

## Backward compatibility

v3 keeps `IEvaluationService.EvaluateAsync` / `EvaluationRequest` (rebuild required after the assembly split). Prefer `Eval.*` and assertions for new code.

`EvaluationRequest.ModelName` maps to `Configuration["Model"]` when Model is unset. `MatchingType = "semantic"` is **TF-IDF**; use `"embedding-semantic"` for embeddings. Unknown matching types fail with a clear error (register a custom metric instead of relying on exact fallback).

## Release notes

**3.2.1** — Dependency refresh (Microsoft.Extensions 10.0.12, Semantic Kernel 1.80.1, test SDK) and README alignment. No API breaks vs 3.2.0.

**3.2.0** — Embeddings semantic metric, opt-in suite run history / HTML trend, QuickStart + OpenAIJudge samples, README/CI/contributor hygiene.

**3.1.0** — Community & polish: CONTRIBUTING, docs, BenchmarkDotNet CI smoke.

**3.0.0** — Core/Abstractions split; Claude / Groq / Mistral; optional Semantic Kernel; `AddLLMEval(IConfiguration)`.

Full details: [`CHANGELOG.md`](CHANGELOG.md). Install: `dotnet add package STAF.LLMEval`
