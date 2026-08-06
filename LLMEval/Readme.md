# STAF.LLMEval

.NET library for evaluating LLM responses against golden outputs and reference documents.

**Package:** [STAF.LLMEval](https://www.nuget.org/packages/STAF.LLMEval) · **Version:** 2.0.0 · **Targets:** `net8.0`, `net9.0`, `net10.0` · **License:** MIT

Supports **Direct** match, **LLM-as-judge**, and **GroundedAnswerCheck** (hallucination / grounding), plus a fluent API, assertions, DI/Options, and JSON/HTML suite reports.

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
await Eval.Direct().Semantic(actual, expected).WithThreshold(0.3).EvaluateAsync(); // TF-IDF
```

## Assertions

```csharp
result.ShouldPass();
result.ShouldScoreAbove(0.8);
result.ShouldBeGrounded(); // fails if RiskLevel is High or unsupported statements exist
```

Failures throw `LLMEvalAssertionException` with score, details, and grounding context.

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
```

Inject an existing service with `.Using(evaluationService)` or apply shared settings with `.WithOptions(options)`.

## Configuration (Options + DI)

```csharp
using Microsoft.Extensions.DependencyInjection;
using LLMEval;

services.AddLLMEval(o =>
{
    o.DefaultProvider = ProviderType.OpenAI;
    o.ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
    o.Model = "gpt-4o-mini";
    o.Temperature = "0";
    o.DefaultPassThreshold = 0.8;
    o.MaxDegreeOfParallelism = 4; // suite case parallelism
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

`EvaluationRequest.ModelName` is copied into `Configuration["Model"]` when Model is not set.

## Core types

### `EvaluationRequest`

- `Question`, `AiResponse`, `GoldenOutput`
- `ProviderType`: `Ollama`, `OpenAI`, `Gemini`, `AzureOpenAI`
- `Endpoint`, `Configuration`, `PassThreshold`, `ModelName`
- `MatchingType`: `exact`, `keyword`, `semantic` (TF-IDF)
- `EvaluationType`: `DirectEvaluation`, `LLMAsJudge`, `GroundedAnswerCheck`
- `IsReferenceDoc` — treat `GoldenOutput` as a reference document for LLM-as-judge
- `ReferenceDocuments` — optional multi-doc list for grounding (overrides `GoldenOutput` when set)

### `EvaluationResult`

- `Score`, `IsPassed`, `Details`, `Confidence`
- Grounding: `UnsupportedStatements`, `PartiallySupportedStatements`, `RiskLevel` (`Low` / `Medium` / `High`)

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

## Suite + HTML/JSON reports

```csharp
var cases = await EvaluationSuite.LoadAsync("cases.json");
// or: EvaluationSuite.ParseDataset(jsonOrJsonl);

var suite = new EvaluationSuite(evalService);
var report = await suite.RunAsync(cases);
await suite.WriteReportsAsync(report, "./artifacts"); // report.json + report.html

if (!report.MeetsPassRate(0.9))
    throw new Exception($"Pass rate {report.PassRate:P0} below threshold");
```

Dataset fields: `id`, `question`, `actual`, `expected`, `evaluationType`, `matchingType`, `threshold`, optional `provider`, `endpoint`, `model`, `isReferenceDoc`, `referenceDocuments`.

Formats: JSON array, JSONL, or `{ "cases": [ ... ] }`.

## Providers

| Provider | Endpoint | Notes |
|----------|----------|--------|
| OpenAI | Optional (defaults to chat completions URL) | `ApiKey`, `Model` |
| Azure OpenAI | Resource root or full chat/completions URL | `Model` = deployment name; optional `ApiVersion` |
| Gemini | Required | `ApiKey` on URL/query as implemented by provider |
| Ollama | e.g. `http://localhost:11434` | Local; `Model` required |

## Notes

- Prefer `Temperature=0` for deterministic judge / grounding runs in CI.
- Default semantic matching uses **TF-IDF**. `GloveModel` / `SemanticSimilarityEvaluator` are obsolete and not used by `AdvancedEvaluationService`.
- Repository: https://github.com/sooraj171/LLMEval
- Changelog / migration: see repo `CHANGELOG.md` (v2.0.0 keeps the classic `EvaluateAsync` API).
