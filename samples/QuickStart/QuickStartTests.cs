using LLMEval;

namespace QuickStart;

/// <summary>
/// Zero-setup DirectEvaluation samples. Run: <c>dotnet test samples/QuickStart/QuickStart.csproj</c>
/// </summary>
public class DirectEvaluationQuickStart
{
    [Fact]
    public async Task ExactMatch_NoApiKey()
    {
        string aiResponse = "Paris";

        var result = await Eval.Direct()
            .Exact(actual: aiResponse, expected: "Paris")
            .WithThreshold(1.0)
            .EvaluateAsync();

        result.ShouldPass();
    }

    [Fact]
    public async Task KeywordAndJson_NoApiKey()
    {
        var keyword = await Eval.Direct()
            .Keyword(
                actual: "The capital of France is Paris",
                expected: "capital France Paris")
            .WithThreshold(0.5)
            .EvaluateAsync();
        keyword.ShouldPass();

        var json = await Eval.Direct()
            .Json("""{"city":"Paris"}""")
            .EvaluateAsync();
        json.ShouldPass();
    }
}
