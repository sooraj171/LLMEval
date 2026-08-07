namespace LLMEval;

/// <summary>
/// Well-known trait / category names for filtering LLM evaluation tests in CI.
/// Framework-agnostic string constants — use with xUnit <c>[Trait]</c>, MSTest <c>[TestCategory]</c>,
/// or NUnit <c>[Category]</c>.
/// </summary>
/// <example>
/// xUnit:
/// <code>
/// [Fact]
/// [Trait(EvalTraits.Category, EvalTraits.LLMEval)]
/// [Trait(EvalTraits.Kind, EvalTraits.Direct)]
/// public async Task ExactMatch_ShouldPass() { ... }
/// </code>
    /// Filter: <c>dotnet test --filter "Category=LLMEval"</c> or <c>"Category=LLMEval&amp;Tag=Smoke"</c>
/// </example>
public static class EvalTraits
{
    /// <summary>Standard trait/category key used by most runners (xUnit Trait name, MSTest category grouping).</summary>
    public const string Category = "Category";

    /// <summary>Optional second trait key for eval kind (Direct / Suite / Baseline / Judge / Grounding).</summary>
    public const string Kind = "Kind";

    /// <summary>Optional third trait key for subsets (Smoke, nightly, etc.).</summary>
    public const string Tag = "Tag";

    /// <summary>Value for Category — marks a test as an LLMEval evaluation.</summary>
    public const string LLMEval = "LLMEval";

    /// <summary>DirectEvaluation / fluent Direct asserts (no provider required for exact/keyword/etc.).</summary>
    public const string Direct = "Direct";

    /// <summary>Evaluation suite / dataset run.</summary>
    public const string Suite = "Suite";

    /// <summary>Golden baseline regression check.</summary>
    public const string Baseline = "Baseline";

    /// <summary>LLM-as-judge evaluation (usually needs API key).</summary>
    public const string Judge = "Judge";

    /// <summary>Grounding / hallucination check (usually needs API key).</summary>
    public const string Grounding = "Grounding";

    /// <summary>Smoke / fast CI subset.</summary>
    public const string Smoke = "Smoke";
}
