# Migrating to STAF.LLMEval 3.0

## Summary

v3 is **source-compatible** for typical `using LLMEval;` callers. Prefer the same one-line install:

```bash
dotnet add package STAF.LLMEval
```

Under the hood, types live in **Abstractions** + **Core** assemblies; the meta package keeps `TypeForwardedTo` shims so existing `LLMEval` assembly references continue to resolve after a rebuild.

## What you must do

1. Bump to **3.0.0** (or later 3.x).
2. Rebuild your project (required after the assembly split).
3. Optionally adopt new providers: `ProviderType.Claude`, `Groq`, `Mistral`.

## What stays the same

- `IEvaluationService.EvaluateAsync` / `EvaluationRequest`
- `Eval.Direct()` / `Judge()` / `Grounding()`
- Assertions, suites, reports, DI `AddLLMEval(Action<LLMEvalOptions>?)`
- Package id **STAF.LLMEval** for the recommended install

## New APIs

| Feature | How |
|---------|-----|
| Claude | `.WithProvider(ProviderType.Claude).WithApiKey(...).WithModel("claude-3-5-haiku-latest")` |
| Groq | `ProviderType.Groq` (default endpoint `https://api.groq.com/openai/v1/chat/completions`) |
| Mistral | `ProviderType.Mistral` (default endpoint `https://api.mistral.ai/v1/chat/completions`) |
| Config binding | `services.AddLLMEval(configuration)` binds section `LLMEval` |
| Semantic Kernel | `dotnet add package STAF.LLMEval.SemanticKernel` → `AddLLMEvalSemanticKernel()` |

## Advanced: reference Core or Abstractions directly

Only needed for multi-package library designs. See [PACKAGES.md](PACKAGES.md).

## Breaking notes

- **Major version:** public types moved to `LLMEval.Abstractions` / `LLMEval.Core` assemblies (namespaces unchanged).
- Recompile is required; copying only the old single `LLMEval.dll` without the new dependencies will fail.
- `ProviderType` gains Claude / Groq / Mistral — switch/exhaustive matches may need updating.
