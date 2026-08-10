using System.Text;
using System.Text.Json;

namespace LLMEval
{
    public class GeminiProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;

        public GeminiProvider(HttpClient httpClient)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<string> GetResponseAsync(string endpoint, string prompt, Dictionary<string, string> configuration, CancellationToken cancellationToken = default)
        {
            if (!configuration.TryGetValue("ApiKey", out var apiKey))
            {
                throw new ArgumentException("Gemini API key is missing in the configuration.");
            }

            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentException("Gemini API endpoint cannot be null or empty.");
            }

            try
            {
                var temperature = 1.0;
                if (configuration.TryGetValue("Temperature", out var tempStr) && double.TryParse(tempStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t))
                {
                    temperature = Math.Clamp(t, 0, 2);
                }

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = temperature
                    }
                };

                var jsonRequestBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

                // Append the API key to the endpoint URL
                var requestUri = $"{endpoint}?key={apiKey}";
                var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                request.Content = content;

                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode(); // Throw an exception for bad status codes

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                if (responseContent == null)
                {
                    throw new InvalidOperationException("Parsed Response is NULL");
                }
                return responseContent;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Error calling Gemini API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception($"Error deserializing Gemini API response: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"An unexpected error occurred while calling Gemini API: {ex.Message}", ex);
            }
        }
    }

    
}
