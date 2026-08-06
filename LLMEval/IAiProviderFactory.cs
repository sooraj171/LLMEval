
namespace LLMEval
{
    public interface IAiProviderFactory
    {
        IAiProvider CreateProvider(ProviderType providerType, HttpClient httpClient);
    }

    public class AiProviderFactory : IAiProviderFactory
    {
        public IAiProvider CreateProvider(ProviderType providerType, HttpClient httpClient)
        {
            switch (providerType)
            {
                case ProviderType.Ollama:
                    return new OllamaProvider(httpClient);
                case ProviderType.OpenAI:
                    return new OpenAIProvider(httpClient);
                case ProviderType.Gemini:
                    return new GeminiProvider(httpClient);
                case ProviderType.AzureOpenAI:
                    return new AzureOpenAIProvider(httpClient);
                default:
                    throw new ArgumentException($"Unsupported provider type: {providerType}");
            }
        }
    }


}
