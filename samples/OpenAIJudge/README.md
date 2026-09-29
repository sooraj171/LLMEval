# OpenAI LLM-as-judge sample

Runs a single `Eval.Judge()` call against OpenAI. **Skipped automatically** when `OPENAI_API_KEY` is not set, so `dotnet test` stays green in CI.

```bash
# PowerShell
$env:OPENAI_API_KEY = "<your key>"
dotnet test samples/OpenAIJudge/OpenAIJudge.csproj
```

```bash
# bash
export OPENAI_API_KEY="<your key>"
dotnet test samples/OpenAIJudge/OpenAIJudge.csproj
```

Uses `gpt-4o-mini` at temperature 0. Change `.WithModel(...)` if you prefer another chat model. Library version: **STAF.LLMEval 3.3.0**.
