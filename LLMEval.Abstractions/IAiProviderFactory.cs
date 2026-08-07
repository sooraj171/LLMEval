namespace LLMEval;

/// <summary>Creates <see cref="IAiProvider"/> instances for a <see cref="ProviderType"/>.</summary>
public interface IAiProviderFactory
{
    IAiProvider CreateProvider(ProviderType providerType, HttpClient httpClient);
}
