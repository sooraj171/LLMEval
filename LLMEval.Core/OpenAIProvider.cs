using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LLMEval
{
    public class OpenAIProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private const string DefaultModel = "gpt-3.5-turbo";

        public OpenAIProvider(HttpClient httpClient)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        public async Task<string> GetResponseAsync(string endpoint, string prompt, Dictionary<string, string> configuration, CancellationToken cancellationToken = default)
        {
            if (!configuration.TryGetValue("ApiKey", out var apiKey))
            {
                throw new ArgumentException("OpenAI API key is missing in the configuration.");
            }

            if (string.IsNullOrEmpty(endpoint))
            {
                endpoint = "https://api.openai.com/v1/chat/completions";
            }
            if (!configuration.TryGetValue("Model", out var model))
            {
                model = DefaultModel;
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
                    model = model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = temperature
                };

                var jsonRequestBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
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
                throw new Exception($"Error calling OpenAI API: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception($"Error deserializing OpenAI API response: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"An unexpected error occurred while calling OpenAI API: {ex.Message}", ex);
            }
        }
    }

    
}
