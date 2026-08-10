namespace LLMEval;

/// <summary>Supported LLM provider backends.</summary>
public enum ProviderType
{
    Ollama,
    OpenAI,
    Gemini,
    AzureOpenAI,
    /// <summary>Anthropic Claude Messages API.</summary>
    Claude,
    /// <summary>Groq OpenAI-compatible chat completions.</summary>
    Groq,
    /// <summary>Mistral AI OpenAI-compatible chat completions.</summary>
    Mistral
}
