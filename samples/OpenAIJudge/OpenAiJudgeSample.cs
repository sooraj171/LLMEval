using LLMEval;

namespace OpenAIJudge;

/// <summary>
/// Live LLM-as-judge sample. Skipped unless <c>OPENAI_API_KEY</c> is set.
/// </summary>
public class OpenAiJudgeSample
{
    [OpenAiFact]
    public async Task Judge_CapitalOfFrance()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;

        var result = await Eval.Judge()
            .WithQuestion("What is the capital of France?")
            .WithResponse("Paris is the capital of France.")
            .WithExpected("Paris")
            .WithProvider(ProviderType.OpenAI)
            .WithApiKey(apiKey)
            .WithModel("gpt-4o-mini")
            .WithTemperature(0)
            .WithThreshold(0.8)
            .EvaluateAsync();

        result.ShouldPass(because: "judge should accept a correct capital answer");
        result.ShouldScoreAbove(0.7);
    }
}

/// <summary>xUnit Fact that skips when OPENAI_API_KEY is unset.</summary>
public sealed class OpenAiFactAttribute : FactAttribute
{
    public OpenAiFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
            Skip = "Set OPENAI_API_KEY to run this sample.";
    }
}
