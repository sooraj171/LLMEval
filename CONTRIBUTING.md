# Contributing to STAF.LLMEval

Thanks for contributing. This guide covers how to propose changes, run tests, and keep releases aligned with the [ROADMAP](ROADMAP.md).

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

## Development setup

```bash
git clone https://github.com/sooraj171/LLMEval.git
cd LLMEval
dotnet restore LLMEval.sln
dotnet test LLMEval.Tests/LLMEval.Tests.csproj -c Release
dotnet test samples/MinimalXunit/MinimalXunit.csproj -c Release --filter "Category=LLMEval"
```

Optional benchmarks (smoke):

```bash
dotnet run -c Release --project benchmarks/LLMEval.Benchmarks -- --filter * --job short --warmupCount 1 --iterationCount 3
```

## Project layout

| Path | Role |
|------|------|
| `LLMEval.Abstractions/` | Contracts & DTOs |
| `LLMEval.Core/` | Engine, providers, suite, reports |
| `LLMEval/` | Meta-package + type forwards (one-line install) |
| `LLMEval.SemanticKernel/` | Optional SK integration |
| `samples/` | MinimalXunit + CI templates |
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

## Release phases

Phases are documented in [ROADMAP.md](ROADMAP.md). Agents and humans: when shipping a phase, update ROADMAP status, [`.cursor/rules/llmeval-releases.mdc`](.cursor/rules/llmeval-releases.mdc), and `CHANGELOG.md`.

## License

By contributing, you agree that your contributions are licensed under the same MIT license as this repository.
