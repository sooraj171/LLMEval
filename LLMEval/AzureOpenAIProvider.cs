using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LLMEval;

/// <summary>Azure OpenAI chat completions provider (deployment name via Configuration["Model"]).</summary>
public class AzureOpenAIProvider : IAiProvider
{
    private readonly HttpClient _httpClient;

    public AzureOpenAIProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetResponseAsync(
        string endpoint,
        string prompt,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Azure OpenAI endpoint is required (resource URL or full chat/completions URL).");

        if (!configuration.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Azure OpenAI API key is missing in the configuration.");

        if (!configuration.TryGetValue("Model", out var deployment) || string.IsNullOrWhiteSpace(deployment))
            throw new ArgumentException("Azure OpenAI deployment name is missing (Configuration[\"Model\"]).");

        var temperature = 1.0;
        if (configuration.TryGetValue("Temperature", out var tempStr)
            && double.TryParse(tempStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t))
        {
            temperature = Math.Clamp(t, 0, 2);
        }

        var apiVersion = "2024-02-15-preview";
        if (configuration.TryGetValue("ApiVersion", out var version) && !string.IsNullOrWhiteSpace(version))
            apiVersion = version;

        var requestUrl = BuildChatCompletionsUrl(endpoint, deployment, apiVersion);

        var requestBody = new
        {
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("api-key", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string BuildChatCompletionsUrl(string endpoint, string deployment, string apiVersion)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.Contains("api-version=", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            var separator = trimmed.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{trimmed}{separator}api-version={apiVersion}";
        }

        return $"{trimmed}/openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version={apiVersion}";
    }
}
