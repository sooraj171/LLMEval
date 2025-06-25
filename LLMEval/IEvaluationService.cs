
namespace LLMEval
{
    public interface IEvaluationService
    {
        Task<EvaluationResult> EvaluateAsync(EvaluationRequest request);
    }

    public class AdvancedEvaluationService : IEvaluationService
    {
        private readonly IAiProviderFactory _providerFactory;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<ProviderType, IAiProvider> _providers;

        public AdvancedEvaluationService(IAiProviderFactory providerFactory)
        {
            _providerFactory = providerFactory;
            _httpClient = new HttpClient();
        }

        public async Task<EvaluationResult> EvaluateAsync(EvaluationRequest request)
        {
            try
            {
                IAiProvider provider = _providerFactory.CreateProvider(request.ProviderType, _httpClient);

                if (request.EvaluationType == EvaluationType.LLMAsJudge)
                {
                    return await EvaluateWithLLMAsync(provider,request);
                }
                else // Default to direct comparison logic
                {
                    return await EvaluateDirectlyAsync(request);
                }
            }
            catch (Exception ex)
            {
                return new EvaluationResult
                {
                    Score = 0,
                    IsPassed = false,
                    Details = $"Evaluation failed: {ex.Message}"
                };
            }
        }

        private async Task<EvaluationResult> EvaluateWithLLMAsync(IAiProvider _provider, EvaluationRequest request)
        {
            string prompt = string.Empty;

            if (request.IsReferenceDoc)
            {
                prompt = $@"Reference Document:
                        {request.GoldenOutput}
                        Question: {request.Question}
                        AI Response: {request.AiResponse}
                        AI's info in Reference Document? (Factual). Score (0-1) (1=all info aligns, 0=no info aligns). Reason:";
                //prompt = $@"Reference Document:
                //    {request.GoldenOutput}
                //    Question: {request.Question}
                //    AI Response: {request.AiResponse}
                //    Based on the provided Reference Document ONLY, does the AI Response contain information that is present in or directly supported by the Reference Document? Evaluate the factual consistency.
                //    Provide a score (0-1). A score of 1 indicates that all factual claims in the AI Response are directly supported by the Reference Document. A score of 0 indicates that the AI Response contains information not found in the Reference Document. Provide a brief reason for the score.";
            }
            else
            {
                prompt = $@"Q: {request.Question}
                            A: {request.AiResponse}
                            E: {request.GoldenOutput}
                            Valid and relevant? (Semantic, common knowledge). Ignore minor format (case, extra info). If unsure, quick fact-check. Score (0-1) and Reason:";
            }
            //else
            //{
            //    prompt = $"Q: {request.Question}\nA: {request.AiResponse}\nE: {request.GoldenOutput}\nEvaluate if A is a valid and relevant answer to Q. Score (0-1) & Reason:";
            //}

            //string prompt = $"Question: {request.Question}\n\nApplication Response: {request.AiResponse}\n\nExpected Golden Response: {request.GoldenOutput}\n\nBased on the question and the expected golden response, evaluate the application's response for validity and relevance. Provide a score (e.g., 0.0 to 1.0) and a brief rationale.";

            try
            {
                string llmResponseJson = await _provider.GetResponseAsync(request.Endpoint, prompt, request.Configuration);

                LLMParseResult parsedResult;
                if (request.ProviderType == ProviderType.Gemini)
                {
                    parsedResult = LLMResponseParser.ParseGeminiEvaluationResponse(llmResponseJson);
                }
                else if (request.ProviderType == ProviderType.Ollama)
                {
                    parsedResult = LLMResponseParser.ParseOllamaEvaluationResponse(llmResponseJson);
                }
                else if (request.ProviderType == ProviderType.OpenAI)
                {
                    parsedResult = LLMResponseParser.ParseOpenAIEvaluationResponse(llmResponseJson);
                }
                else
                {
                    return new EvaluationResult { Score = 0, IsPassed = false, Details = "Unsupported LLM provider for parsing." };
                }

                double score = 0;
                double.TryParse(parsedResult.ScoreString, out score);
                bool isPassed = score >= request.PassThreshold;

                // Potentially adjust isPassed based on the reasoning for IsReferenceDocument
                if (request.IsReferenceDoc && parsedResult.Description.ToLower().Contains("not found in document"))
                {
                    isPassed = false; // Or adjust score accordingly
                    score = 0;
                }

                string confidence = score >=0.8 ? "High" : score >= .5 ? "Medium" : "Low";

                return new EvaluationResult
                {
                    Score = score,
                    IsPassed = isPassed,
                    Details = parsedResult.Description,
                    Confidence = confidence
                };
            }
            catch (Exception ex)
            {
                return new EvaluationResult
                {
                    Score = 0,
                    IsPassed = false,
                    Details = $"Error during LLM evaluation: {ex.Message}"
                };
            }
        }

        private async Task<EvaluationResult> EvaluateDirectlyAsync(EvaluationRequest request)
        {
            // ... (Your previous direct comparison logic - ExactMatchScore, KeywordMatchScore, SemanticSimilarityScore)
            double score = 0;
            string details = null;

            switch (request.MatchingType?.ToLower())
            {
                case "keyword":
                    score = KeywordMatchScore(request.AiResponse, request.GoldenOutput);
                    break;
                case "semantic":
                    score = await SemanticSimilarityScore(request.AiResponse, request.GoldenOutput);
                    break;
                default:
                    score = ExactMatchScore(request.AiResponse, request.GoldenOutput);
                    break;
            }

            bool isPassed = score >= request.PassThreshold;

            return new EvaluationResult
            {
                Score = score,
                IsPassed = isPassed,
                Details = details
            };
        }

        private double ExactMatchScore(string response, string golden)
        {
            return response.Trim().Equals(golden.Trim(), StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        }

        private double KeywordMatchScore(string response, string golden)
        {
            var responseKeywords = response.ToLower().Split(new[] { ' ', '-', ',', '.', ';', ':' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var goldenKeywords = golden.ToLower().Split(new[] { ' ', '-', ',', '.', ';', ':' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

            if (!goldenKeywords.Any()) return 1.0; // If no expected keywords, consider it a match

            int matchedKeywords = 0;
            foreach (var keyword in goldenKeywords)
            {
                if (responseKeywords.Contains(keyword))
                {
                    matchedKeywords++;
                }
            }

            return (double)matchedKeywords / goldenKeywords.Count;
        }

        private async Task<double> SemanticSimilarityScore(string response, string golden)
        {
            // This is a placeholder for a more advanced implementation.
            // It would likely involve:
            // 1. Using an NLP library or service to get text embeddings for both strings.
            // 2. Calculating the cosine similarity between the embeddings.
            // Consider libraries like NLTK (Python, but could be interfaced), or cloud-based services.
            // Fact-checking against a knowledge base would be even more complex.
            Console.WriteLine("Semantic similarity is not yet implemented.");
            return 0.5; // Placeholder score
        }
    }
}
