namespace LLMEval
{
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
                case ProviderType.Claude:
                    return new ClaudeProvider(httpClient);
                case ProviderType.Groq:
                    return new GroqProvider(httpClient);
                case ProviderType.Mistral:
                    return new MistralProvider(httpClient);
                default:
                    throw new ArgumentException($"Unsupported provider type: {providerType}");
            }
        }
    }
}
