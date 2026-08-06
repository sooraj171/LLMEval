# STAF.LLMEval

**STAF.LLMEval** is a .NET **LLM evaluation** and **AI testing framework** for scoring and validating **GenAI / ChatGPT / GPT** responses in **unit tests** and **CI/CD**.

Use it to run **exact match**, **semantic similarity**, **LLM-as-judge**, and **RAG groundedness / hallucination detection** against golden answers or reference documents—with a fluent API, assertions, evaluation suites, and HTML/JSON reports. Works with **OpenAI**, **Azure OpenAI**, **Gemini**, and **Ollama** on **.NET 8 / 9 / 10**.

[![NuGet](https://img.shields.io/nuget/v/STAF.LLMEval.svg)](https://www.nuget.org/packages/STAF.LLMEval)
**v2.0.1** · **Targets:** `net8.0`, `net9.0`, `net10.0` · **License:** MIT · [Release notes](CHANGELOG.md)

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
| **Direct** (`exact` / `keyword` / `semantic` TF-IDF) | `Eval.Direct()` | Deterministic golden-answer checks, no LLM cost |
| **LLM-as-judge** | `Eval.Judge()` | Semantic quality scoring via a provider |
| **GroundedAnswerCheck** | `Eval.Grounding()` | RAG hallucination checks — each claim vs reference docs |

## Fluent API + assertions

```csharp
// Direct
await Eval.Direct().Exact("Paris", "Paris").EvaluateAsync();
await Eval.Direct().Keyword(actual, expected).WithThreshold(0.5).EvaluateAsync();
await Eval.Direct().Semantic(actual, expected).WithThreshold(0.3).EvaluateAsync();

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

// Assertions
result.ShouldPass();
result.ShouldScoreAbove(0.8);
result.ShouldBeGrounded(); // RiskLevel != High, no unsupported statements
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

var eval = sp.GetRequiredService<IEvaluationService>();
```

Classic API still works: `EvaluationRequest` + `AdvancedEvaluationService.EvaluateAsync`.

## Suite reports (CI)

```csharp
var cases = await EvaluationSuite.LoadAsync("cases.json");
var suite = new EvaluationSuite(evalService);
var report = await suite.RunAsync(cases);
await suite.WriteReportsAsync(report, "artifacts"); // report.json + report.html

Assert.True(report.MeetsPassRate(0.9));
```

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

Also supports JSONL and `{ "cases": [ ... ] }`.

## Providers

| Provider | Notes |
|----------|--------|
| **OpenAI** | Default chat completions endpoint if `Endpoint` empty |
| **Azure OpenAI** | Resource URL + deployment name in `Model` |
| **Gemini** | Requires endpoint + API key |
| **Ollama** | Local, e.g. `http://localhost:11434` |

## Samples & docs

| Doc | Purpose |
|-----|---------|
| [`samples/MinimalXunit`](samples/MinimalXunit) | Copy-paste xUnit sample (no API keys) |
| [`LLMEval/Readme.md`](LLMEval/Readme.md) | Full package / API guide (also on NuGet) |
| [`CHANGELOG.md`](CHANGELOG.md) | v2.0.0 / v2.0.1 release notes |
| [`ROADMAP.md`](ROADMAP.md) | Release phases (Phase 1 done; Phase 2 next) |

## Backward compatibility

v2.0 keeps `IEvaluationService.EvaluateAsync` / `EvaluationRequest`. Prefer `Eval.*` and assertions for new code.

`EvaluationRequest.ModelName` maps to `Configuration["Model"]` when Model is unset. Semantic Direct matching uses **TF-IDF** (`MatchingType = "semantic"`); the old GloVe helpers are obsolete.

## Release notes (v2.0.1)

**2.0.1** — NuGet metadata/tags/README discoverability only (same APIs as 2.0.0).

**2.0.0** — Multi-target `net8.0` / `net9.0` / `net10.0`; fluent `Eval.Direct()` / `Judge()` / `Grounding()`; assertions; DI/Options; JSON/JSONL suites + HTML/JSON reports; Azure OpenAI; classic API unchanged.

Full details: [`CHANGELOG.md`](CHANGELOG.md). Install: `dotnet add package STAF.LLMEval --version 2.0.1`
