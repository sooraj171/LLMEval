## Summary

<!-- What does this PR change, and why? -->

## Checklist

- [ ] `dotnet test LLMEval.Tests/LLMEval.Tests.csproj` is green (net8 / net9 / net10)
- [ ] Sample projects still run (`samples/QuickStart` at minimum)
- [ ] Public API is additive (`EvaluateAsync` / `EvaluationRequest` / `Eval.*` / `MetricRegistry` unchanged unless discussed)
- [ ] New public Core types are type-forwarded from `LLMEval/TypeForwards.cs`
- [ ] Docs / CHANGELOG updated when behavior or API changes
- [ ] No secrets, keys, or live credentials in tests or samples

## Test plan

<!-- How did you verify this? Unit tests, `dotnet test samples/QuickStart`, manual report.html, … -->
