using System.Text.Json;
using System.Text.RegularExpressions;

namespace LLMEval
{
    public class LLMResponseParser
    {
        /// <summary>
        /// Parses the Gemini evaluation response JSON string and extracts the score and description.
        /// </summary>
        /// <param name="jsonResponse"></param>
        /// <returns></returns>
        public static LLMParseResult ParseGeminiEvaluationResponse(string jsonResponse)
        {
            try
            {
                var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(jsonResponse);

                if (geminiResponse?.candidates != null && geminiResponse.candidates.Length > 0 &&
                    geminiResponse.candidates[0].content?.parts != null && geminiResponse.candidates[0].content.parts.Length > 0)
                {
                    string llmOutput = geminiResponse.candidates[0].content.parts[0].text;
                    return ParseLLMOutput(llmOutput);
                }
                else
                {
                    return new LLMParseResult { ScoreString = "0", Description = "Could not extract evaluation from LLM response." };
                }
            }
            catch (JsonException ex)
            {
                return new LLMParseResult { ScoreString = "0", Description = $"Error deserializing LLM response: {ex.Message}\nRaw Response: {jsonResponse}" };
            }
            catch (Exception ex)
            {
                return new LLMParseResult { ScoreString = "0", Description = $"An unexpected error occurred while parsing the LLM response: {ex.Message}\nRaw Response: {jsonResponse}" };
            }
        }

        private static LLMParseResult ParseLLMOutput(string llmOutput)
        {
            string scoreString = "0";
            string description = llmOutput.Trim();

            // Try to find a numerical score at the beginning, optionally followed by a period
            var scoreMatch = Regex.Match(llmOutput, @"^([0-9]+(\.[0-9]+)?)\s*(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            if (scoreMatch.Success)
            {
                scoreString = scoreMatch.Groups[1].Value;
                description = scoreMatch.Groups[3].Value.Trim();
            }
            else
            {
                // If no score at the beginning, try to find "Score:" followed by a number
                var laterScoreMatch = Regex.Match(llmOutput, @"Score:\s*([0-9.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (laterScoreMatch.Success)
                {
                    scoreString = laterScoreMatch.Groups[1].Value;
                    // The rest of the output might be the description
                    description = llmOutput.Substring(laterScoreMatch.Index + laterScoreMatch.Length).Trim();
                }
                else if (llmOutput.ToLower().Contains("exact match"))
                {
                    scoreString = "1.0";
                }
                else if (llmOutput.ToLower().Contains("partial match"))
                {
                    scoreString = "0.5";
                }
                // If no score is found, the entire output is treated as the description, and score remains "0"
            }

            return new LLMParseResult
            {
                ScoreString = scoreString,
                Description = description
            };
        }

        public static LLMParseResult ParseOllamaEvaluationResponse(string jsonResponse)
        {
            try
            {
                var ollamaResponse = JsonSerializer.Deserialize<OllamaEvaluationResponse>(jsonResponse);

                if (!string.IsNullOrEmpty(ollamaResponse?.response))
                {
                    return ParseLLMOutput(ollamaResponse.response);
                }
                else
                {
                    return new LLMParseResult { ScoreString = "0", Description = "Could not extract evaluation from LLM response." };
                }
            }
            catch (JsonException ex)
            {
                return new LLMParseResult { ScoreString = "0", Description = $"Error deserializing LLM response: {ex.Message}\nRaw Response: {jsonResponse}" };
            }
            catch (Exception ex)
            {
                return new LLMParseResult { ScoreString = "0", Description = $"An unexpected error occurred while parsing the LLM response: {ex.Message}\nRaw Response: {jsonResponse}" };
            }
        }

        public static LLMParseResult ParseOpenAIEvaluationResponse(string jsonResponse)
        {
            try
            {
                var openAIResponse = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(jsonResponse);
                if (openAIResponse?.choices != null && openAIResponse.choices.Length > 0 &&
                    openAIResponse.choices[0].message?.content != null)
                {
                    return ParseLLMOutput(openAIResponse.choices[0].message.content);
                }
                else
                {
                    return new LLMParseResult { ScoreString = "0", Description = "Could not extract evaluation from LLM response." };
                }
            }
            catch (JsonException ex)
            {
                return new LLMParseResult { ScoreString = "0", Description = $"Error deserializing LLM response: {ex.Message}\nRaw Response: {jsonResponse}" };
            }
            catch (Exception ex)
            {
                return new LLMParseResult { ScoreString = "0", Description = $"An unexpected error occurred while parsing the LLM response: {ex.Message}\nRaw Response: {jsonResponse}" };
            }
        }
    }

    // Define the necessary classes to deserialize the Gemini response
    public class GeminiResponse
    {
        public Candidate[]? candidates { get; set; }
    }

    public class Candidate
    {
        public Content? content { get; set; }
    }

    public class Content
    {
        public Part[]? parts { get; set; }
    }

    public class Part
    {
        public string? text { get; set; }
    }

    // New class to hold the parsed LLM response
    public class LLMParseResult
    {
        public string ScoreString { get; set; }
        public string Description { get; set; }
    }

    // Define the necessary class to deserialize the Ollama response
    public class OllamaEvaluationResponse
    {
        public string? model { get; set; }
        public string? created_at { get; set; }
        public string? response { get; set; }
        public bool done { get; set; }
        public string? done_reason { get; set; }
        public List<int>? context { get; set; }
        // You might need to add other properties if Ollama's response includes more relevant data
    }

    // Define structures to match the expected OpenAI Chat Completion API response
    public class OpenAIChatCompletionResponse
    {
        public Choice[]? choices { get; set; }
    }

    public class Choice
    {
        public Message? message { get; set; }
    }

    public class Message
    {
        public string? role { get; set; }
        public string? content { get; set; }
    }
}
