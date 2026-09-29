# STAF.LLMEval packages

| Package | Role | Install |
|---------|------|---------|
| **[STAF.LLMEval](https://www.nuget.org/packages/STAF.LLMEval)** | **Meta-package (recommended)** — one-line install; type-forwards into Core | `dotnet add package STAF.LLMEval` |
| [STAF.LLMEval.Core](https://www.nuget.org/packages/STAF.LLMEval.Core) | Evaluation engine, providers, suite/reports (incl. opt-in run history), fluent API, TF-IDF + embeddings metrics | pulled by meta |
| [STAF.LLMEval.Abstractions](https://www.nuget.org/packages/STAF.LLMEval.Abstractions) | Contracts & DTOs (`IEvaluationService`, `IEmbeddingProvider`, `EvaluationRequest`, …) | pulled by Core |
| [STAF.LLMEval.SemanticKernel](https://www.nuget.org/packages/STAF.LLMEval.SemanticKernel) | Optional Semantic Kernel `IAiProvider` adapter (SK 1.80.1) | `dotnet add package STAF.LLMEval.SemanticKernel` |

## When to reference what

- **Apps / tests:** install **STAF.LLMEval** only (same as v2).
- **Library authors** who want zero engine coupling: reference **Abstractions** and accept `IEvaluationService` / `IEvaluationMetric`.
- **Semantic Kernel hosts:** add **STAF.LLMEval.SemanticKernel** and call `AddLLMEvalSemanticKernel()`.

## Assembly layout (v3)

| Assembly | Contents |
|----------|----------|
| `LLMEval.Abstractions` | `ProviderType`, `EvaluationRequest`/`Result`, `IAiProvider*`, `IEmbeddingProvider`, `IEvaluationService`, metrics contracts, `LLMEvalOptions` (incl. `EnableRunHistory`) |
| `LLMEval.Core` | `AdvancedEvaluationService`, `Eval.*` (incl. `EmbeddingSemantic`), providers, suite, asserts, metrics implementations |
| `LLMEval` | Meta assembly with `TypeForwardedTo` shims (binary-friendly upgrade path) |
| `LLMEval.SemanticKernel` | `SemanticKernelChatProvider`, `AddLLMEvalSemanticKernel` |

Namespaces remain **`LLMEval`** (and `LLMEval.Integrations.SemanticKernel` for the SK package).

## Aspire / ASP.NET Core

No separate Aspire package in v3. Use configuration binding:

```csharp
services.AddLLMEval(builder.Configuration); // binds section "LLMEval"
```

Playwright / MCP remain out of scope until requested (see ROADMAP). Current package version: **3.2.1**.
