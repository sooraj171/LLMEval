namespace LLMEval
{
    public interface IEvaluationService
    {
        Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default);
    }

    public class AdvancedEvaluationService : IEvaluationService
    {
        private readonly IAiProviderFactory _providerFactory;
        private readonly HttpClient _httpClient;
        private readonly TfidfSimilarity _tfidfSimilarity;

        public AdvancedEvaluationService(IAiProviderFactory providerFactory)
            : this(providerFactory, new HttpClient())
        {
        }

        public AdvancedEvaluationService(IAiProviderFactory providerFactory, HttpClient httpClient)
        {
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _tfidfSimilarity = new TfidfSimilarity();
        }

        public async Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                IAiProvider provider = _providerFactory.CreateProvider(request.ProviderType, _httpClient);

                if (request.EvaluationType == EvaluationType.GroundedAnswerCheck)
                {
                    return await EvaluateGroundedAnswerCheckAsync(provider, request, cancellationToken);
                }
                if (request.EvaluationType == EvaluationType.LLMAsJudge)
                {
                    return await EvaluateWithLLMAsync(provider, request, cancellationToken);
                }
                return await EvaluateDirectlyAsync(request);
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

        private async Task<EvaluationResult> EvaluateGroundedAnswerCheckAsync(IAiProvider provider, EvaluationRequest request, CancellationToken cancellationToken)
        {
            string referenceText = ResolveReferenceText(request);
            if (referenceText.Length > ResponseStatementSplitter.MaxReferenceLength)
            {
                referenceText = referenceText.Substring(0, ResponseStatementSplitter.MaxReferenceLength) + "...";
            }

            var config = new Dictionary<string, string>(request.Configuration);
            if (!config.ContainsKey("Temperature"))
            {
                config["Temperature"] = "0";
            }

            var statements = ResponseStatementSplitter.SplitIntoStatements(request.AiResponse);
            if (statements.Count == 0)
            {
                return new EvaluationResult
                {
                    Score = 1.0,
                    IsPassed = true,
                    Details = "No factual statements to validate.",
                    UnsupportedStatements = Array.Empty<string>(),
                    PartiallySupportedStatements = Array.Empty<string>(),
                    RiskLevel = "Low"
                };
            }

            var unsupported = new List<string>();
            var partiallySupported = new List<string>();
            int supportedCount = 0;

            const string systemInstruction = "You are a grounding validator. Given REFERENCE TEXT and one CLAIM from an AI response, output exactly one of: SUPPORTED, PARTIALLY_SUPPORTED, or UNSUPPORTED. Then optionally one line starting with Reason:";
            string refBlock = $"REFERENCE TEXT:\n{referenceText}";

            foreach (var statement in statements)
            {
                string prompt = $"{systemInstruction}\n\n{refBlock}\n\nCLAIM to check: \"{statement.Replace("\"", "'")}\"\n\nYour classification (one word):";
                string jsonResponse;
                try
                {
                    jsonResponse = await provider.GetResponseAsync(request.Endpoint, prompt, config, cancellationToken);
                }
                catch (Exception ex)
                {
                    return new EvaluationResult
                    {
                        Score = 0,
                        IsPassed = false,
                        Details = $"Judge call failed for statement: {ex.Message}",
                        UnsupportedStatements = unsupported,
                        PartiallySupportedStatements = partiallySupported,
                        RiskLevel = "High"
                    };
                }

                string? rawContent = GetRawContentFromProvider(request.ProviderType, jsonResponse);
                var label = GroundingJudgeParser.ParseGroundingJudgeOutput(rawContent ?? string.Empty);

                switch (label)
                {
                    case GroundingLabel.Supported:
                        supportedCount++;
                        break;
                    case GroundingLabel.PartiallySupported:
                        partiallySupported.Add(statement);
                        break;
                    default:
                        unsupported.Add(statement);
                        break;
                }
            }

            double groundingScore = (double)supportedCount / statements.Count;
            string riskLevel = ComputeRiskLevel(supportedCount, partiallySupported.Count, unsupported.Count, statements.Count);
            bool isPassed = riskLevel != "High" && groundingScore >= request.PassThreshold;

            string details = $"Grounding: {supportedCount}/{statements.Count} statements fully supported ({groundingScore:P0}). Unsupported: {unsupported.Count}; Partial: {partiallySupported.Count}. Risk: {riskLevel}.";

            return new EvaluationResult
            {
                Score = groundingScore,
                IsPassed = isPassed,
                Confidence = riskLevel == "Low" ? "High" : riskLevel == "Medium" ? "Medium" : "Low",
                Details = details,
                UnsupportedStatements = unsupported,
                PartiallySupportedStatements = partiallySupported,
                RiskLevel = riskLevel
            };
        }

        private static string ResolveReferenceText(EvaluationRequest request)
        {
            if (request.ReferenceDocuments != null && request.ReferenceDocuments.Count > 0)
            {
                return string.Join("\n\n", request.ReferenceDocuments);
            }
            return request.GoldenOutput ?? string.Empty;
        }

        private static string? GetRawContentFromProvider(ProviderType providerType, string jsonResponse)
        {
            return providerType switch
            {
                ProviderType.Gemini => LLMResponseParser.GetRawContentFromGeminiResponse(jsonResponse),
                ProviderType.Ollama => LLMResponseParser.GetRawContentFromOllamaResponse(jsonResponse),
                ProviderType.OpenAI => LLMResponseParser.GetRawContentFromOpenAIResponse(jsonResponse),
                _ => null
            };
        }

        private static string ComputeRiskLevel(int supportedCount, int partialCount, int unsupportedCount, int total)
        {
            if (total == 0) return "Low";
            if (unsupportedCount > 0) return "High";
            if (partialCount > 0) return "Medium";
            return "Low";
        }

        private async Task<EvaluationResult> EvaluateWithLLMAsync(IAiProvider provider, EvaluationRequest request, CancellationToken cancellationToken)
        {
            string prompt = request.IsReferenceDoc
                ? $@"Reference Document:
                        {request.GoldenOutput}
                        Question: {request.Question}
                        AI Response: {request.AiResponse}
                        AI's info in Reference Document? (Factual). Score (0-1) (1=all info aligns, 0=no info aligns). Reason:"
                : $@"Q: {request.Question}
                            A: {request.AiResponse}
                            E: {request.GoldenOutput}
                            Valid and relevant? (Semantic, common knowledge). Ignore minor format (case, extra info). If unsure, quick fact-check. Score (0-1) and Reason:";

            try
            {
                string llmResponseJson = await provider.GetResponseAsync(request.Endpoint, prompt, request.Configuration, cancellationToken);

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

                if (request.IsReferenceDoc && (parsedResult.Description?.ToLower().Contains("not found in document") ?? false))
                {
                    isPassed = false; // Or adjust score accordingly
                    score = 0;
                }

                string confidence = score >=0.8 ? "High" : score >= .5 ? "Medium" : "Low";

                return new EvaluationResult
                {
                    Score = score,
                    IsPassed = isPassed,
                    Details = parsedResult.Description ?? string.Empty,
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

        private Task<EvaluationResult> EvaluateDirectlyAsync(EvaluationRequest request)
        {
            double score;
            string details = string.Empty;

            switch (request.MatchingType?.ToLower())
            {
                case "keyword":
                    score = KeywordMatchScore(request.AiResponse, request.GoldenOutput);
                    break;
                case "semantic":
                    (score, details) = _tfidfSimilarity.Calculate(request.AiResponse, request.GoldenOutput);
                    break;
                default:
                    score = ExactMatchScore(request.AiResponse, request.GoldenOutput);
                    break;
            }

            bool isPassed = score >= request.PassThreshold;

            return Task.FromResult(new EvaluationResult
            {
                Score = score,
                IsPassed = isPassed,
                Details = details
            });
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

    }
}
