namespace LLMEval;

/// <summary>Typed configuration for STAF.LLMEval (Options pattern).</summary>
public class LLMEvalOptions
{
    public const string SectionName = "LLMEval";

    /// <summary>Default provider used when a request does not specify one.</summary>
    public ProviderType DefaultProvider { get; set; } = ProviderType.OpenAI;

    /// <summary>API endpoint (required for Azure OpenAI, Gemini, Ollama; optional for OpenAI).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key for cloud providers.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model or Azure deployment name.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Sampling temperature as string for provider config (e.g. "0").</summary>
    public string Temperature { get; set; } = "0";

    /// <summary>Default pass threshold (0–1).</summary>
    public double DefaultPassThreshold { get; set; } = 0.8;

    /// <summary>Max concurrent judge calls when running suites / grounding.</summary>
    public int MaxDegreeOfParallelism { get; set; } = 4;

    /// <summary>Builds the provider <see cref="Dictionary{TKey,TValue}"/> expected by existing providers.</summary>
    public Dictionary<string, string> ToConfigurationDictionary()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(ApiKey))
            dict["ApiKey"] = ApiKey;
        if (!string.IsNullOrWhiteSpace(Model))
            dict["Model"] = Model;
        if (!string.IsNullOrWhiteSpace(Temperature))
            dict["Temperature"] = Temperature;
        return dict;
    }
}
