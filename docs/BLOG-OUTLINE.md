# Blog & extra sample outline

Content ideas for posts and companion repos. Implementation may live **outside** this repository.

## Blog series (suggested)

1. **“LLM eval in xUnit in 5 minutes”** — Direct exact match, `ShouldPass`, zero API keys; link MinimalXunit.
2. **“RAG grounding in CI”** — `Eval.Grounding()`, pass-rate gates, report artifacts.
3. **“Golden datasets that don’t flake”** — CSV/JSONL, baseline comparison, TF-IDF thresholds.
4. **“From one package to a modular stack”** — v3 Abstractions / Core / meta / SemanticKernel.
5. **“Judge cost control”** — when not to use LLM-as-judge; Temperature=0; suite tagging.

## Companion sample repos (optional)

| Repo idea | Focus |
|-----------|--------|
| `llmeval-aspnet-sample` | `AddLLMEval(IConfiguration)` + minimal API that scores chat replies |
| `llmeval-sk-sample` | Semantic Kernel chat + `AddLLMEvalSemanticKernel` |
| `llmeval-azure-devops-demo` | Forkable pipeline using `samples/ci/azure-pipelines-llmeval.yml` |

Keep secrets out of samples; document env vars only.

## Talk / demo script (10 minutes)

1. Show failing Direct assert with rich message  
2. Fix expected / threshold; re-run green  
3. Run suite → open STAF-style `report.html`  
4. Mention baseline + `ShouldMeetPassRate` for CI  

## Tracking

Open a GitHub Discussion under **Ideas** when starting a companion repo or blog draft so the community can vote on topics.
