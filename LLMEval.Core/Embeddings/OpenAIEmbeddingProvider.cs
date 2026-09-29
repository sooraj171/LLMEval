using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LLMEval;

/// <summary>
/// OpenAI and Azure OpenAI embeddings API. Construct with configuration from
/// <see cref="MetricContext"/> (ApiKey, Model / EmbeddingModel, Endpoint).
/// </summary>
public sealed class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    public const string DefaultModel = "text-embedding-3-small";
    public const string DefaultAzureApiVersion = "2024-02-01";

    private readonly HttpClient _httpClient;
    private readonly IReadOnlyDictionary<string, string> _configuration;
    private readonly string? _endpoint;
    private readonly ProviderType _providerType;

    public OpenAIEmbeddingProvider(
        HttpClient httpClient,
        IReadOnlyDictionary<string, string> configuration,
        string? endpoint = null,
        ProviderType providerType = default)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _endpoint = endpoint;
        _providerType = providerType;
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
            return Array.Empty<float[]>();

        if (!TryGet( "ApiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Embeddings API key is missing (Configuration[\"ApiKey\"]).");

        var endpoint = ResolveEndpoint();
        var model = ResolveModel();
        var azure = IsAzure(endpoint);

        if (azure && string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Azure OpenAI embeddings deployment name is missing (Configuration[\"EmbeddingModel\"] or [\"Model\"]).");

        var url = azure
            ? BuildAzureEmbeddingsUrl(endpoint!, model, ResolveAzureApiVersion())
            : BuildOpenAiEmbeddingsUrl(endpoint);

        var bodyModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        var requestBody = new Dictionary<string, object?>
        {
            ["input"] = texts.ToArray(),
            ["model"] = bodyModel
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (azure)
            request.Headers.TryAddWithoutValidation("api-key", apiKey);
        else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Embeddings API returned {(int)response.StatusCode}: {payload}");

        return ParseEmbeddingsResponse(payload, texts.Count);
    }

    public static string BuildOpenAiEmbeddingsUrl(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return "https://api.openai.com/v1/embeddings";

        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.Contains("/embeddings", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return trimmed + "/embeddings";
        return trimmed + "/v1/embeddings";
    }

    public static string BuildAzureEmbeddingsUrl(string endpoint, string deployment, string apiVersion)
    {
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.Contains("/embeddings", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.Contains("api-version=", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            var separator = trimmed.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{trimmed}{separator}api-version={apiVersion}";
        }

        return $"{trimmed}/openai/deployments/{Uri.EscapeDataString(deployment)}/embeddings?api-version={apiVersion}";
    }

    internal static IReadOnlyList<float[]> ParseEmbeddingsResponse(string json, int expectedCount)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Embeddings response did not contain a data array.");

        var scored = new List<(int Index, float[] Vector)>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            var index = item.TryGetProperty("index", out var idxEl) && idxEl.TryGetInt32(out var i) ? i : scored.Count;
            if (!item.TryGetProperty("embedding", out var embEl) || embEl.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Embeddings response item was missing an embedding array.");

            var vector = new float[embEl.GetArrayLength()];
            var n = 0;
            foreach (var v in embEl.EnumerateArray())
            {
                vector[n++] = v.GetSingle();
            }
            scored.Add((index, vector));
        }

        var ordered = scored.OrderBy(s => s.Index).Select(s => s.Vector).ToArray();
        if (expectedCount > 0 && ordered.Length != expectedCount)
            throw new InvalidOperationException($"Expected {expectedCount} embeddings, received {ordered.Length}.");
        return ordered;
    }

    internal bool IsAzure(string? endpoint)
    {
        if (_providerType == ProviderType.OpenAI)
            return endpoint?.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase) == true
                   || endpoint?.Contains("/openai/deployments/", StringComparison.OrdinalIgnoreCase) == true;
        if (_providerType == ProviderType.AzureOpenAI)
            return true;

        if (TryGet("EmbeddingProvider", out var embedProvider))
        {
            if (embedProvider.Equals("openai", StringComparison.OrdinalIgnoreCase))
                return false;
            if (embedProvider.Equals("azure", StringComparison.OrdinalIgnoreCase)
                || embedProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (TryGet("ProviderType", out var providerType)
            && providerType.Equals(nameof(ProviderType.AzureOpenAI), StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        return endpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase)
               || endpoint.Contains("/openai/deployments/", StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveEndpoint()
    {
        if (TryGet("EmbeddingEndpoint", out var embedEp) && !string.IsNullOrWhiteSpace(embedEp))
            return embedEp;
        if (!string.IsNullOrWhiteSpace(_endpoint))
            return _endpoint;
        if (TryGet("Endpoint", out var ep) && !string.IsNullOrWhiteSpace(ep))
            return ep;
        return _endpoint;
    }

    private string ResolveModel()
    {
        if (TryGet("EmbeddingModel", out var embedModel) && !string.IsNullOrWhiteSpace(embedModel))
            return embedModel;
        if (TryGet("EmbeddingDeployment", out var deploy) && !string.IsNullOrWhiteSpace(deploy))
            return deploy;
        if (TryGet("Model", out var model) && !string.IsNullOrWhiteSpace(model))
            return model;
        return DefaultModel;
    }

    private string ResolveAzureApiVersion()
    {
        if (TryGet("ApiVersion", out var version) && !string.IsNullOrWhiteSpace(version))
            return version;
        return DefaultAzureApiVersion;
    }

    private bool TryGet(string key, out string value)
    {
        foreach (var kv in _configuration)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }
        value = "";
        return false;
    }
}
