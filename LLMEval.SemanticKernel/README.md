# STAF.LLMEval.SemanticKernel

Optional [Semantic Kernel](https://github.com/microsoft/semantic-kernel) integration for **STAF.LLMEval** **3.3.0** (depends on `Microsoft.SemanticKernel.Abstractions` 1.80.1).

```bash
dotnet add package STAF.LLMEval
dotnet add package STAF.LLMEval.SemanticKernel
```

```csharp
using LLMEval.Integrations.SemanticKernel;
using Microsoft.SemanticKernel;

// After registering Kernel / IChatCompletionService:
services.AddLLMEvalSemanticKernel(options =>
{
    options.DefaultPassThreshold = 0.8;
});

// Or construct directly:
IAiProvider provider = new SemanticKernelChatProvider(kernel);
IAiProviderFactory factory = new SemanticKernelProviderFactory(kernel);
var eval = new AdvancedEvaluationService(factory);
```

**LLM-as-judge** and **Grounding** use the Kernel chat service. **DirectEvaluation** (exact / keyword / TF-IDF `Semantic` / `EmbeddingSemantic` / JSON / schema) does not require Semantic Kernel.

See the [root README](../README.md) for embeddings, suite reports, and opt-in run history.
