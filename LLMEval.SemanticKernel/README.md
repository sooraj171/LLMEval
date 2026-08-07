# STAF.LLMEval.SemanticKernel

Optional [Semantic Kernel](https://github.com/microsoft/semantic-kernel) integration for **STAF.LLMEval**.

```bash
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

Judge and grounding calls use the Kernel chat service; DirectEvaluation metrics do not require SK.
