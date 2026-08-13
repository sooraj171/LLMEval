# STAF.LLMEval

**STAF.LLMEval** is a .NET **LLM evaluation** and **AI testing framework** for scoring and validating **GenAI / ChatGPT / GPT** responses in **unit tests** and **CI/CD**.

Use it to run **exact match**, **semantic similarity**, **LLM-as-judge**, and **RAG groundedness / hallucination detection** against golden answers or reference documents—with a fluent API, assertions, evaluation suites, and HTML/JSON reports. Works with **OpenAI**, **Azure OpenAI**, **Gemini**, **Ollama**, **Claude**, **Groq**, and **Mistral** on **.NET 8 / 9 / 10**.

[![NuGet](https://img.shields.io/badge/NuGet-v3.1.0-0B3D91?logo=nuget&logoColor=white)](https://www.nuget.org/packages/STAF.LLMEval)
**v3.1.0** · **Targets:** `net8.0`, `net9.0`, `net10.0` · **License:** MIT · [Release notes](CHANGELOG.md) · [Packages](docs/PACKAGES.md) · [Migrate to v3](docs/MIGRATION-v3.md) · [Contributing](CONTRIBUTING.md)

## Who is this for?

- Teams building RAG chatbots who need **grounding** and **hallucination** checks in CI  
- Developers who want to **evaluate AI responses** like unit tests (`ShouldPass`, `ShouldBeGrounded`)  
- Anyone searching for **.NET LLM evaluation**, **AI response evaluation**, **prompt/model evaluation**, or **golden-dataset regression**

## 5-minute start (no API key)

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

```bash
dotnet test samples/MinimalXunit/MinimalXunit.csproj
```

## What you can evaluate

| Mode | Fluent entry | Use when |
|------|----------------|----------|
| **Direct** (`exact` / `keyword` / `semantic` TF-IDF / `json` / `schema` / `relevance` / `grounded-heuristic`) | `Eval.Direct()` | Deterministic golden-answer & structure checks, no LLM cost |
| **LLM-as-judge** | `Eval.Judge()` | Semantic quality scoring via a provider |
| **GroundedAnswerCheck** | `Eval.Grounding()` | RAG hallucination checks — each claim vs reference docs |

## Fluent API + assertions

```csharp
// Direct
await Eval.Direct().Exact("Paris", "Paris").EvaluateAsync();
await Eval.Direct().Keyword(actual, expected).WithThreshold(0.5).EvaluateAsync();
await Eval.Direct().Semantic(actual, expected).WithThreshold(0.3).EvaluateAsync(); // TF-IDF (not embeddings)
await Eval.Direct().Json("""{"ok":true}""").EvaluateAsync();
await Eval.Direct().Schema(actualJson, jsonSchema).EvaluateAsync();
await Eval.Direct().Relevance(question, actual).WithThreshold(0.2).EvaluateAsync();
await Eval.Direct().GroundedHeuristic(actual, reference).WithThreshold(0.5).EvaluateAsync();

// Judge / grounding (needs provider + key or Ollama)
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

// Claude / Groq / Mistral
await Eval.Judge()
    .WithQuestion(q).WithResponse(actual).WithExpected(expected)
    .WithProvider(ProviderType.Claude)
    .WithApiKey(anthropicKey).WithModel("claude-3-5-haiku-latest")
    .EvaluateAsync();

// Assertions
result.ShouldPass(because: "exact capital");
result.ShouldScoreAbove(0.8);
result.ShouldBeGrounded(); // RiskLevel != High, no unsupported statements

// Filter eval tests in CI: [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
// dotnet test --filter "Category=LLMEval"  or  "Category=LLMEval&Tag=Smoke"
```

## DI / Options

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddLLMEval(options =>
{
    options.DefaultProvider = ProviderType.AzureOpenAI;
    options.Endpoint = "https://YOUR_RESOURCE.openai.azure.com";
    options.ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;
    options.Model = "gpt-4o-mini"; // Azure deployment name
    options.DefaultPassThreshold = 0.8;
});

// ASP.NET / Aspire host: bind section "LLMEval"
services.AddLLMEval(builder.Configuration);

var eval = sp.GetRequiredService<IEvaluationService>();
```

Classic API still works: `EvaluationRequest` + `AdvancedEvaluationService.EvaluateAsync`.

Optional Semantic Kernel: `dotnet add package STAF.LLMEval.SemanticKernel` then `services.AddLLMEvalSemanticKernel(...)`.

## Suite reports (CI)

```csharp
var cases = await EvaluationSuite.LoadAsync("cases.json"); // also .jsonl / .csv
var smoke = cases.FilterByTags("smoke"); // optional dataset tags
var suite = new EvaluationSuite(evalService);
var report = await suite.RunAsync(smoke);
var outDir = ReportPaths.ResolveReportDirectory("artifacts"); // or set LLMEVAL_REPORT_DIR
await suite.WriteReportsAsync(report, outDir); // report.json + .html + .md + .csv
// report.html uses the STAF HtmlResult skin (blue header, cyan rows, green/red PASS/FAIL)

report.ShouldMeetPassRate(0.9, because: "CI pass-rate threshold");

// Golden baseline regression (compare to a checked-in report.json)
var diff = await BaselineComparer.CompareToBaselineFileAsync(report, "baseline-report.json");
Assert.False(diff.HasRegressions, diff.ToSummary());
```

Official pipeline templates (pass-rate fail + report artifacts): [`samples/ci`](samples/ci) (GitHub Actions + Azure DevOps).

Example `cases.json`:

```json
[
  {
    "id": "capital-exact",
    "question": "What is the capital of France?",
    "actual": "Paris",
    "expected": "Paris",
    "evaluationType": "DirectEvaluation",
    "matchingType": "exact",
    "threshold": 1.0
  }
]
```

Also supports JSONL, CSV (header row), and `{ "cases": [ ... ] }`.

### Custom metrics (no fork)

```csharp
services.AddLLMEvalMetric<MyMetric>();
services.AddLLMEval(configureMetrics: registry => registry.Register(new MyMetric()));

// or without DI:
var registry = MetricRegistry.CreateDefault();
registry.Register(new MyMetric());
var service = new AdvancedEvaluationService(new AiProviderFactory(), new HttpClient(), null, registry);
```

## Providers

| Provider | Notes |
|----------|--------|
| **OpenAI** | Default chat completions endpoint if `Endpoint` empty |
| **Azure OpenAI** | Resource URL + deployment name in `Model` |
| **Gemini** | Requires endpoint + API key |
| **Ollama** | Local, e.g. `http://localhost:11434` |
| **Claude** | Anthropic Messages API |
| **Groq** | OpenAI-compatible (`api.groq.com`) |
| **Mistral** | OpenAI-compatible (`api.mistral.ai`) |

Optional cost estimate when usage is present: set `Configuration["InputCostPer1M"]` / `OutputCostPer1M` (USD per 1M tokens).

## Samples & docs

| Doc | Purpose |
|-----|---------|
| [`samples/MinimalXunit`](samples/MinimalXunit) | Copy-paste xUnit sample (no API keys) + traits + baseline CI check |
| [`samples/ci`](samples/ci) | GitHub Actions + Azure DevOps templates (threshold fail + artifacts) |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | How to contribute + GitHub Discussions prompt |
| [`docs/BEST-PRACTICES.md`](docs/BEST-PRACTICES.md) | Eval / CI best practices |
| [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md) | Cost model, parallelism, benchmarks |
| [`docs/BLOG-OUTLINE.md`](docs/BLOG-OUTLINE.md) | Blog / companion sample ideas |
| [`docs/PACKAGES.md`](docs/PACKAGES.md) | Multi-package layout (meta / Core / Abstractions / SK) |
| [`docs/MIGRATION-v3.md`](docs/MIGRATION-v3.md) | Upgrade guide from 2.x → 3.0 |
| [`benchmarks/LLMEval.Benchmarks`](benchmarks/LLMEval.Benchmarks) | BenchmarkDotNet hot-path suite |
| [`LLMEval/Readme.md`](LLMEval/Readme.md) | Full package / API guide (also on NuGet) |
| [`CHANGELOG.md`](CHANGELOG.md) | Release notes |
| [`ROADMAP.md`](ROADMAP.md) | Release phases (Phase 5 done; maintenance) |

## Backward compatibility

v3 keeps `IEvaluationService.EvaluateAsync` / `EvaluationRequest` (rebuild required after assembly split). Prefer `Eval.*` and assertions for new code.

`EvaluationRequest.ModelName` maps to `Configuration["Model"]` when Model is unset. Semantic Direct matching uses **TF-IDF** (`MatchingType = "semantic"`); the old GloVe helpers are obsolete. Unknown matching types fail with a clear error (register a custom metric instead of relying on exact fallback).

## Release notes (v3.1.0)

**3.1.0** — Community & polish: CONTRIBUTING, best practices / performance docs, blog outline, BenchmarkDotNet CI smoke, NuGet metadata refresh.

**3.0.0** — Core/Abstractions split + meta package type-forwards; Claude / Groq / Mistral; optional Semantic Kernel package; `AddLLMEval(IConfiguration)`.

**2.2.0** — Richer asserts + `ShouldMeetPassRate`, `EvalTraits` / suite tags, `LLMEVAL_REPORT_DIR`, GitHub Actions & Azure DevOps CI templates with report artifacts.

**2.1.0** — Pluggable metrics (json/schema/relevance/heuristic grounding), CSV datasets, baseline comparison, Markdown/CSV reports, token/cost usage fields.

**2.0.1** — NuGet metadata/tags/README discoverability only (same APIs as 2.0.0).

**2.0.0** — Multi-target `net8.0` / `net9.0` / `net10.0`; fluent `Eval.Direct()` / `Judge()` / `Grounding()`; assertions; DI/Options; JSON/JSONL suites + HTML/JSON reports; Azure OpenAI; classic API unchanged.

Full details: [`CHANGELOG.md`](CHANGELOG.md). Install: `dotnet add package STAF.LLMEval --version 3.1.0`
