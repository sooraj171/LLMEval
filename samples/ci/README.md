# CI templates for STAF.LLMEval (Phase 3 / v2.2)

Official pipeline samples that:

1. Run evaluation tests (xUnit sample by default)
2. Fail the job when assertions / `ShouldMeetPassRate` fail
3. Publish suite report artifacts (`report.json`, `.html`, `.md`, `.csv`)

| File | Platform |
|------|----------|
| [github-actions-llmeval.yml](github-actions-llmeval.yml) | GitHub Actions |
| [azure-pipelines-llmeval.yml](azure-pipelines-llmeval.yml) | Azure DevOps |

## Pass-rate failure threshold

In tests:

```csharp
report.ShouldMeetPassRate(0.9, because: "CI pass-rate threshold");
```

The pipeline does not need a separate script — a failed assertion fails `dotnet test` (exit code ≠ 0).

## Report artifacts

Set `LLMEVAL_REPORT_DIR` (e.g. `artifacts/llmeval`). The sample suite writes reports via:

```csharp
var outDir = ReportPaths.ResolveReportDirectory(fallback);
await suite.WriteReportsAsync(report, outDir);
```

Then upload / publish that folder as a pipeline artifact.

## Filtering eval tests

Use [`EvalTraits`](../../LLMEval/EvalTraits.cs) with xUnit `[Trait]`:

```bash
dotnet test --filter "Category=LLMEval"
dotnet test --filter "Category=LLMEval&Tag=Smoke"
```

Filter suite **cases** by dataset tags:

```csharp
var smoke = cases.FilterByTags("smoke"); // or EvalTraits.Smoke
```

## Wire into this repo

The main [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) already runs the MinimalXunit sample and uploads reports. Copy these templates into consumer repos and point `dotnet test` at your own test project.
