namespace LLMEval;

/// <summary>Low-level LLM HTTP provider used by judge and grounding evaluation.</summary>
public interface IAiProvider
{
    Task<string> GetResponseAsync(
        string endpoint,
        string prompt,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default);
}
