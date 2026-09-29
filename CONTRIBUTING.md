# Contributing to STAF.LLMEval

Thanks for contributing. This guide covers how to build, test, add a metric or provider, and open a pull request. Keep releases aligned with the [ROADMAP](ROADMAP.md).

## Ways to participate

| Channel | Use for |
|---------|---------|
| [GitHub Issues](https://github.com/sooraj171/LLMEval/issues) | Bugs, feature requests, provider gaps |
| [GitHub Discussions](https://github.com/sooraj171/LLMEval/discussions) | Design questions, “how do I…?”, RFCs |
| Pull requests | Concrete code/docs changes |

### Start a Discussion first when…

- You are unsure which package to change (`STAF.LLMEval` vs Core vs Abstractions vs SemanticKernel)
- The change would break `IEvaluationService.EvaluateAsync` / `EvaluationRequest`
- You want a new provider, Aspire/Playwright/MCP integration, or a major API surface

**Suggested first Discussion title:** `RFC: <short idea>` — include motivation, proposed API sketch, and whether it can stay source-compatible.

> **Repo maintainers:** enable **Discussions** on the GitHub repo (Settings → General → Features → Discussions) if not already on. Pin an “Announcements” category and a “Q&A” category for support.

## How to build

```bash
git clone https://github.com/sooraj171/LLMEval.git
cd LLMEval
dotnet restore LLMEval.sln
dotnet build LLMEval.sln -c Release
```

Library projects target `net8.0;net9.0;net10.0`. You need those SDKs installed (CI uses `actions/setup-dotnet` with 8/9/10).

## How to run tests

```bash
dotnet test LLMEval.Tests/LLMEval.Tests.csproj -c Release
dotnet test samples/QuickStart/QuickStart.csproj -c Release
dotnet test samples/MinimalXunit/MinimalXunit.csproj -c Release --filter "Category=LLMEval"
```

Live OpenAI judge sample (skipped automatically when the key is missing):

```bash
# PowerShell
$env:OPENAI_API_KEY = "<key>"
dotnet test samples/OpenAIJudge/OpenAIJudge.csproj -c Release
```

Optional benchmarks (smoke):

```bash
dotnet run -c Release --project benchmarks/LLMEval.Benchmarks -- --filter * --job short --warmupCount 1 --iterationCount 3
```

## Adding a new IEvaluationMetric

DirectEvaluation scores go through `IEvaluationMetric` + `MetricRegistry`. Do **not** fork `AdvancedEvaluationService`.

1. Implement `IEvaluationMetric` in `LLMEval.Core` (or your own assembly):

```csharp
public sealed class MyMetric : IEvaluationMetric
{
    public string Name => "my-metric"; // becomes MatchingType

    public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        var score = /* 0–1 */;
        return Task.FromResult(new MetricResult
        {
            Score = score,
            IsPassed = score >= context.PassThreshold,
            Details = "…"
        });
    }
}
```

2. Register it (pick one):

```csharp
// Built-in: MetricRegistry.RegisterBuiltIns()
// App / tests:
registry.Register(new MyMetric());
services.AddLLMEvalMetric<MyMetric>();
services.AddLLMEval(configureMetrics: r => r.Register(new MyMetric()));
```

3. Call it with `Eval.Direct().WithMetric("my-metric", actual, expected)` or a small fluent helper on `DirectEvaluationBuilder` if it belongs in the public API.

4. Add unit tests in `LLMEval.Tests` (no live network). If the metric needs HTTP, inject a collaborator (`IEmbeddingProvider` is the embeddings example) or stub `HttpMessageHandler`.

5. If you add a public type in Core, add a `[assembly: TypeForwardedTo(typeof(...))]` in `LLMEval/TypeForwards.cs` so the meta-package keeps working.

## Adding a new provider

Judge and grounding calls go through `IAiProvider` + `AiProviderFactory`.

1. Add a value to `ProviderType` in Abstractions (this is additive; exhaustive `switch`es in **callers** may need updating — document it in CHANGELOG).
2. Implement `IAiProvider` in `LLMEval.Core` (see `OpenAICompatibleProviders.cs` for Groq/Mistral-style APIs).
3. Wire it in `AiProviderFactory.CreateProvider`.
4. Parse the chat JSON in `LLMResponseParser` if the payload is not OpenAI-shaped.
5. Mock HTTP in `LLMEval.Tests` (see `StubHttpHandler` in `Phase1Tests.cs`). Do not commit API keys.
6. Type-forward the new provider class from the meta-package.
7. Document the provider in the README providers table.

Embeddings are a separate plug-in (`IEmbeddingProvider` / `OpenAIEmbeddingProvider`); they are not `IAiProvider` chat backends.

## Project layout

| Path | Role |
|------|------|
| `LLMEval.Abstractions/` | Contracts & DTOs |
| `LLMEval.Core/` | Engine, providers, suite, reports |
| `LLMEval/` | Meta-package + type forwards (one-line install) |
| `LLMEval.SemanticKernel/` | Optional SK integration |
| `samples/` | QuickStart, OpenAIJudge, MinimalXunit, CI templates |
| `docs/` | Packages, migration, best practices, performance |
| `benchmarks/` | BenchmarkDotNet hot-path suite |

See [docs/PACKAGES.md](docs/PACKAGES.md).

## Coding guidelines

- Keep **`IEvaluationService.EvaluateAsync` / `EvaluationRequest`** working unless a ROADMAP phase explicitly allows breaks.
- Prefer **async-first**, nullable enabled, XML docs on public APIs.
- Multi-TFM: `net8.0;net9.0;net10.0` must stay green.
- Do not invent scope beyond the current ROADMAP phase unless discussed.
- Match existing style; avoid drive-by refactors unrelated to the PR.

## Pull request checklist

- [ ] Tests added/updated; `dotnet test` green on net8/net9/net10
- [ ] Docs/CHANGELOG touched when behavior or public API changes
- [ ] No secrets in samples or tests
- [ ] Package version / ROADMAP updates only when shipping a release phase
- [ ] New public Core types are type-forwarded from `STAF.LLMEval`

## Release phases

Phases are documented in [ROADMAP.md](ROADMAP.md). Agents and humans: when shipping a phase, update ROADMAP status, [`.cursor/rules/llmeval-releases.mdc`](.cursor/rules/llmeval-releases.mdc), and `CHANGELOG.md`.

## License

By contributing, you agree that your contributions are licensed under the same MIT license as this repository.
