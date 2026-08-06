# Changelog

All notable changes to STAF.LLMEval are documented here.

## [2.0.0] - 2026-08-06

### Added

- Multi-target frameworks: `net8.0`, `net9.0`, `net10.0`
- Fluent API: `Eval.Direct()`, `Eval.Judge()`, `Eval.Grounding()`
- Assertions: `ShouldPass()`, `ShouldScoreAbove()`, `ShouldBeGrounded()` (`LLMEvalAssertionException`)
- Options + DI: `LLMEvalOptions`, `services.AddLLMEval(...)`
- Suite runner: `EvaluationSuite` (JSON / JSONL) with `report.json` + `report.html`
- Azure OpenAI provider (`ProviderType.AzureOpenAI`)
- PR CI workflow (build + test); publish workflow updated for multi-TFM + symbols
- Root README, MinimalXunit sample, ROADMAP

### Changed

- Package version **2.0.0**; SourceLink, XML docs, symbol packages enabled
- `ModelName` is applied to `Configuration["Model"]` when Model is unset
- Grounding judge calls remain sequential (correct claim ordering); suite runs use bounded parallelism

### Obsolete

- `GloveModel` and `SemanticSimilarityEvaluator` — not wired into `AdvancedEvaluationService`; use `MatchingType = "semantic"` (TF-IDF)

### Migration from 1.x

- Existing `EvaluationRequest` + `EvaluateAsync` code continues to work
- Prefer `Eval.*` and assertions for new tests
- If you only targeted `net10.0`, you can now also consume from net8/net9 projects
- Fix any docs/code that used `IsReferenceDocument` — the property is `IsReferenceDoc`

## [1.5.0] - prior

- DirectEvaluation, LLMAsJudge, GroundedAnswerCheck
- Providers: OpenAI, Gemini, Ollama
