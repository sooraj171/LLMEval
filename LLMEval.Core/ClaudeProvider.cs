using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LLMEval;

/// <summary>Anthropic Claude Messages API provider.</summary>
public class ClaudeProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private const string DefaultEndpoint = "https://api.anthropic.com/v1/messages";
    private const string DefaultModel = "claude-3-5-haiku-latest";
    private const string DefaultApiVersion = "2023-06-01";

    public ClaudeProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetResponseAsync(
        string endpoint,
        string prompt,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Claude API key is missing in the configuration.");

        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = DefaultEndpoint;

        if (!configuration.TryGetValue("Model", out var model) || string.IsNullOrWhiteSpace(model))
            model = DefaultModel;

        var apiVersion = DefaultApiVersion;
        if (configuration.TryGetValue("ApiVersion", out var version) && !string.IsNullOrWhiteSpace(version))
            apiVersion = version;

        var maxTokens = 1024;
        if (configuration.TryGetValue("MaxTokens", out var maxTokensStr)
            && int.TryParse(maxTokensStr, out var parsedMax)
            && parsedMax > 0)
        {
            maxTokens = parsedMax;
        }

        double? temperature = null;
        if (configuration.TryGetValue("Temperature", out var tempStr)
            && double.TryParse(tempStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t))
        {
            temperature = Math.Clamp(t, 0, 1);
        }

        var requestBody = temperature.HasValue
            ? (object)new
            {
                model,
                max_tokens = maxTokens,
                temperature = temperature.Value,
                messages = new[] { new { role = "user", content = prompt } }
            }
            : new
            {
                model,
                max_tokens = maxTokens,
                messages = new[] { new { role = "user", content = prompt } }
            };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", apiVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
