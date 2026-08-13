# LLMEval.Benchmarks

BenchmarkDotNet suite for STAF.LLMEval hot paths (Direct metrics, JSON dataset parse, STAF HTML report).

```bash
dotnet run -c Release --project benchmarks/LLMEval.Benchmarks
```

CI runs the same command as a short in-process smoke. See [docs/PERFORMANCE.md](../../docs/PERFORMANCE.md).
