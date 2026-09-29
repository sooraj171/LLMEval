namespace LLMEval;

/// <summary>
/// Pluggable embedding backend used by <c>embedding-semantic</c> DirectEvaluation.
/// Implementations return one vector per input text, in the same order.
/// </summary>
public interface IEmbeddingProvider
{
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
