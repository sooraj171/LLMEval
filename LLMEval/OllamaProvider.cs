using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LLMEval
{
    public class OllamaProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;

        public OllamaProvider(HttpClient httpClient)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<string> GetResponseAsync(string endpoint, string prompt, Dictionary<string, string> configuration, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentException("Ollama API endpoint cannot be null or empty.");
            }
            if (!configuration.TryGetValue("Model", out var model))
            {
                throw new ArgumentException("Ollama model is missing in the configuration.");
            }

            try
            {
                // Create the request payload
                var request = new OllamaRequest
                {
                    Model = model,
                    Prompt = prompt,
                    Stream = false  // Ensure we get a complete response
                };

                // Serialize the request
                var jsonContent = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Ensure endpoint is properly formatted (remove trailing slash if present)
                endpoint = endpoint.TrimEnd('/');

                var response = await _httpClient.PostAsync($"{endpoint}/api/generate", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException($"Error response from Ollama API: {response.StatusCode}, Content: {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return responseContent ?? "No response generated.";
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error calling Ollama API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception($"Error deserializing Ollama API response: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"An unexpected error occurred while calling Ollama API: {ex.Message}", ex);
            }
        }
    }

    public class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("done")]
        public bool Done { get; set; }

        [JsonPropertyName("total_duration")]
        public long? TotalDuration { get; set; }

        [JsonPropertyName("load_duration")]
        public long? LoadDuration { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("prompt_eval_duration")]
        public long? PromptEvalDuration { get; set; }

        [JsonPropertyName("eval_count")]
        public int? EvalCount { get; set; }

        [JsonPropertyName("eval_duration")]
        public long? EvalDuration { get; set; }
    }
    public class OllamaRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;
    }

    

}
