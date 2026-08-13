# STAF.LLMEval performance guide

How to keep evaluation suites fast and predictable in CI.

## Cost model

| Mode | Relative cost | Notes |
|------|---------------|--------|
| Direct (`exact`, `keyword`, `json`, `schema`) | Lowest | CPU only |
| Direct (`semantic` TF-IDF, `relevance`, `grounded-heuristic`) | Low–medium | CPU; grows with text length |
| LLM-as-judge | High | Network + tokens |
| Grounding (per-claim judge calls) | Highest | One judge call **per statement**, sequential |

Prefer Direct for the bulk of regression cases; sample a smaller set with judge/grounding.

## Suite parallelism

- Suite cases run with bounded parallelism (`LLMEvalOptions.MaxDegreeOfParallelism`, default 4).
- Grounding **claims within a case** stay sequential (correct claim mapping).
- Increase parallelism only when providers and rate limits allow.

## Hot paths (benchmarked)

The `benchmarks/LLMEval.Benchmarks` project measures:

- Exact / keyword / semantic metrics
- Dataset parse (JSON)
- STAF HTML report generation (`HtmlResult`)

Run locally:

```bash
dotnet run -c Release --project benchmarks/LLMEval.Benchmarks
```

CI runs a short smoke (`--job short`) to catch accidental regressions without long wall-clock time.

## Reporting

- Prefer writing reports once per suite run (`WriteReportsAsync`).
- HTML uses the STAF skin; JSON/MD/CSV are cheaper to generate — all four are written today.
- Large `Details` / expected / actual text inflate HTML size; truncate in datasets when possible.

## Token usage

When providers return usage, results populate `TokenUsage`. Set `InputCostPer1M` / `OutputCostPer1M` in configuration for estimated USD cost in reports.

## Caching tips (app-side)

STAF.LLMEval does not cache provider calls. For expensive judges:

- Cache by `(model, prompt hash)` in your test host when outputs are stable.
- Skip live judge tests behind an env flag (`LLMEVAL_LIVE=1`) in PR CI.

## Further reading

- [BEST-PRACTICES.md](BEST-PRACTICES.md)
- [samples/ci](../samples/ci)
