namespace LLMEval;

/// <summary>Fluent entry point for evaluations (zero-ceremony DirectEvaluation first).</summary>
public static class Eval
{
    /// <summary>Starts a direct (no LLM) evaluation builder.</summary>
    public static DirectEvaluationBuilder Direct() => new();

    /// <summary>Starts an LLM-as-judge evaluation builder.</summary>
    public static JudgeEvaluationBuilder Judge() => new();

    /// <summary>Starts a grounded-answer / hallucination check builder.</summary>
    public static GroundingEvaluationBuilder Grounding() => new();
}

/// <summary>Shared fluent configuration for builders that call a judge provider.</summary>
public abstract class EvaluationBuilderBase<TSelf>
    where TSelf : EvaluationBuilderBase<TSelf>
{
    protected EvaluationRequest Request { get; } = new();
    protected IEvaluationService? Service { get; private set; }
    protected LLMEvalOptions? Options { get; private set; }

    protected TSelf Self => (TSelf)this;

    public TSelf WithQuestion(string question)
    {
        Request.Question = question;
        return Self;
    }

    public TSelf WithResponse(string aiResponse)
    {
        Request.AiResponse = aiResponse;
        return Self;
    }

    public TSelf WithExpected(string goldenOutput)
    {
        Request.GoldenOutput = goldenOutput;
        return Self;
    }

    public TSelf WithThreshold(double threshold)
    {
        Request.PassThreshold = threshold;
        return Self;
    }

    public TSelf WithProvider(ProviderType providerType)
    {
        Request.ProviderType = providerType;
        return Self;
    }

    public TSelf WithEndpoint(string endpoint)
    {
        Request.Endpoint = endpoint;
        return Self;
    }

    public TSelf WithModel(string model)
    {
        Request.ModelName = model;
        Request.Configuration["Model"] = model;
        return Self;
    }

    public TSelf WithApiKey(string apiKey)
    {
        Request.Configuration["ApiKey"] = apiKey;
        return Self;
    }

    public TSelf WithTemperature(double temperature)
    {
        Request.Configuration["Temperature"] = temperature.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Self;
    }

    public TSelf WithConfiguration(IDictionary<string, string> configuration)
    {
        foreach (var kv in configuration)
            Request.Configuration[kv.Key] = kv.Value;
        return Self;
    }

    public TSelf WithOptions(LLMEvalOptions options)
    {
        Options = options;
        ApplyOptions(options);
        return Self;
    }

    public TSelf Using(IEvaluationService service)
    {
        Service = service;
        return Self;
    }

    protected void ApplyOptions(LLMEvalOptions options)
    {
        if (string.IsNullOrEmpty(Request.Endpoint) && !string.IsNullOrEmpty(options.Endpoint))
            Request.Endpoint = options.Endpoint;

        if (Request.PassThreshold <= 0 && options.DefaultPassThreshold > 0)
            Request.PassThreshold = options.DefaultPassThreshold;

        // Only apply default provider when caller has not set endpoint/provider for judge flows.
        // DirectEvaluation ignores provider; for judge/grounding, Prefer options when Configuration empty and Endpoint empty.
        if (string.IsNullOrEmpty(Request.Endpoint) && Request.Configuration.Count == 0)
            Request.ProviderType = options.DefaultProvider;
        else if (!string.IsNullOrEmpty(options.Endpoint) || options.DefaultProvider != ProviderType.Ollama)
        {
            // If user set WithProvider already, leave it; otherwise use options default when still at enum default and options differ.
        }

        foreach (var kv in options.ToConfigurationDictionary())
        {
            if (!Request.Configuration.ContainsKey(kv.Key))
                Request.Configuration[kv.Key] = kv.Value;
        }
    }

    protected async Task<EvaluationResult> EvaluateCoreAsync(CancellationToken cancellationToken)
    {
        if (Options != null)
            ApplyOptions(Options);

        if (Request.PassThreshold <= 0)
            Request.PassThreshold = 0.8;

        AdvancedEvaluationService.ApplyModelNameToConfiguration(Request);

        var service = Service ?? new AdvancedEvaluationService(new AiProviderFactory(), new HttpClient(), Options);
        return await service.EvaluateAsync(Request, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Fluent builder for <see cref="EvaluationType.DirectEvaluation"/>.</summary>
public sealed class DirectEvaluationBuilder : EvaluationBuilderBase<DirectEvaluationBuilder>
{
    public DirectEvaluationBuilder()
    {
        Request.EvaluationType = EvaluationType.DirectEvaluation;
        Request.MatchingType = "exact";
        Request.PassThreshold = 1.0;
    }

    /// <summary>Exact (case-insensitive) match between actual and expected.</summary>
    public DirectEvaluationBuilder Exact(string actual, string expected)
    {
        Request.AiResponse = actual;
        Request.GoldenOutput = expected;
        Request.MatchingType = "exact";
        return this;
    }

    public DirectEvaluationBuilder Keyword(string actual, string expected)
    {
        Request.AiResponse = actual;
        Request.GoldenOutput = expected;
        Request.MatchingType = "keyword";
        return this;
    }

    public DirectEvaluationBuilder Semantic(string actual, string expected)
    {
        Request.AiResponse = actual;
        Request.GoldenOutput = expected;
        Request.MatchingType = "semantic";
        return this;
    }

    /// <summary>Validates that <paramref name="actual"/> is parseable JSON.</summary>
    public DirectEvaluationBuilder Json(string actual)
    {
        Request.AiResponse = actual;
        Request.MatchingType = "json";
        Request.PassThreshold = 1.0;
        return this;
    }

    /// <summary>Validates <paramref name="actual"/> JSON against a JSON Schema.</summary>
    public DirectEvaluationBuilder Schema(string actual, string jsonSchema)
    {
        Request.AiResponse = actual;
        Request.Schema = jsonSchema;
        Request.GoldenOutput = jsonSchema;
        Request.MatchingType = "schema";
        Request.PassThreshold = 1.0;
        return this;
    }

    /// <summary>Scores relevance of the response to the question (TF-IDF).</summary>
    public DirectEvaluationBuilder Relevance(string question, string actual, string? expected = null)
    {
        Request.Question = question;
        Request.AiResponse = actual;
        if (expected != null) Request.GoldenOutput = expected;
        Request.MatchingType = "relevance";
        return this;
    }

    /// <summary>Lightweight heuristic grounding (no LLM) of actual vs reference.</summary>
    public DirectEvaluationBuilder GroundedHeuristic(string actual, string reference)
    {
        Request.AiResponse = actual;
        Request.GoldenOutput = reference;
        Request.MatchingType = "grounded-heuristic";
        return this;
    }

    /// <summary>Uses a custom or built-in metric by name (must be registered on the service registry).</summary>
    public DirectEvaluationBuilder WithMetric(string metricName, string actual, string expected)
    {
        Request.AiResponse = actual;
        Request.GoldenOutput = expected;
        Request.MatchingType = metricName;
        return this;
    }

    public Task<EvaluationResult> EvaluateAsync(CancellationToken cancellationToken = default)
        => EvaluateCoreAsync(cancellationToken);
}

/// <summary>Fluent builder for <see cref="EvaluationType.LLMAsJudge"/>.</summary>
public sealed class JudgeEvaluationBuilder : EvaluationBuilderBase<JudgeEvaluationBuilder>
{
    public JudgeEvaluationBuilder()
    {
        Request.EvaluationType = EvaluationType.LLMAsJudge;
        Request.PassThreshold = 0.8;
    }

    public JudgeEvaluationBuilder AsReferenceDoc(bool isReferenceDoc = true)
    {
        Request.IsReferenceDoc = isReferenceDoc;
        return this;
    }

    public Task<EvaluationResult> EvaluateAsync(CancellationToken cancellationToken = default)
        => EvaluateCoreAsync(cancellationToken);
}

/// <summary>Fluent builder for <see cref="EvaluationType.GroundedAnswerCheck"/>.</summary>
public sealed class GroundingEvaluationBuilder : EvaluationBuilderBase<GroundingEvaluationBuilder>
{
    public GroundingEvaluationBuilder()
    {
        Request.EvaluationType = EvaluationType.GroundedAnswerCheck;
        Request.PassThreshold = 0.8;
        Request.Configuration["Temperature"] = "0";
    }

    public GroundingEvaluationBuilder WithReferenceDocuments(params string[] documents)
    {
        Request.ReferenceDocuments = documents;
        return this;
    }

    public Task<EvaluationResult> EvaluateAsync(CancellationToken cancellationToken = default)
        => EvaluateCoreAsync(cancellationToken);
}
