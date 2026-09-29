namespace LLMEval;

/// <summary>
/// Backends for the opt-in <c>semantic-embedding</c> metric.
/// Mapped onto <see cref="ProviderType"/> by <c>WithProvider(EmbeddingProviderType)</c>.
/// </summary>
public enum EmbeddingProviderType
{
    /// <summary>OpenAI embeddings API (<c>text-embedding-3-small</c> by default).</summary>
    OpenAI,

    /// <summary>Azure OpenAI embeddings. <c>Model</c> is the deployment name.</summary>
    AzureOpenAI
}
