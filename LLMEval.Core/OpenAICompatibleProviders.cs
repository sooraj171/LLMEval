using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LLMEval;

/// <summary>Shared OpenAI-compatible chat completions client (Groq, Mistral, etc.).</summary>
public abstract class OpenAICompatibleProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _defaultEndpoint;
    private readonly string _defaultModel;
    private readonly string _providerName;

    protected OpenAICompatibleProvider(
        HttpClient httpClient,
        string providerName,
        string defaultEndpoint,
        string defaultModel)
    {
        _httpClient = httpClient ?? new HttpClient();
        _providerName = providerName;
        _defaultEndpoint = defaultEndpoint;
        _defaultModel = defaultModel;
    }

    public async Task<string> GetResponseAsync(
        string endpoint,
        string prompt,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException($"{_providerName} API key is missing in the configuration.");

        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = _defaultEndpoint;

        if (!configuration.TryGetValue("Model", out var model) || string.IsNullOrWhiteSpace(model))
            model = _defaultModel;

        var temperature = 1.0;
        if (configuration.TryGetValue("Temperature", out var tempStr)
            && double.TryParse(tempStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t))
        {
            temperature = Math.Clamp(t, 0, 2);
        }

        var requestBody = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Groq OpenAI-compatible chat completions provider.</summary>
public sealed class GroqProvider : OpenAICompatibleProvider
{
    public GroqProvider(HttpClient httpClient)
        : base(httpClient, "Groq", "https://api.groq.com/openai/v1/chat/completions", "llama-3.3-70b-versatile")
    {
    }
}

/// <summary>Mistral AI OpenAI-compatible chat completions provider.</summary>
public sealed class MistralProvider : OpenAICompatibleProvider
{
    public MistralProvider(HttpClient httpClient)
        : base(httpClient, "Mistral", "https://api.mistral.ai/v1/chat/completions", "mistral-small-latest")
    {
    }
}
