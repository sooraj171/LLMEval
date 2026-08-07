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
        private readonly LLMEvalOptions? _options;
        private readonly MetricRegistry _metrics;

        public AdvancedEvaluationService(IAiProviderFactory providerFactory)
            : this(providerFactory, new HttpClient(), null, null)
        {
        }

        public AdvancedEvaluationService(IAiProviderFactory providerFactory, HttpClient httpClient)
            : this(providerFactory, httpClient, null, null)
        {
        }

        public AdvancedEvaluationService(IAiProviderFactory providerFactory, HttpClient httpClient, LLMEvalOptions? options)
            : this(providerFactory, httpClient, options, null)
        {
        }

        public AdvancedEvaluationService(
            IAiProviderFactory providerFactory,
            HttpClient httpClient,
            LLMEvalOptions? options,
            MetricRegistry? metrics)
        {
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options;
            _metrics = metrics ?? MetricRegistry.CreateDefault();
        }

        /// <summary>Metric registry used for DirectEvaluation (built-ins + custom registrations).</summary>
        public MetricRegistry Metrics => _metrics;

        /// <summary>Maps <see cref="EvaluationRequest.ModelName"/> into Configuration["Model"] when Model is unset.</summary>
        public static void ApplyModelNameToConfiguration(EvaluationRequest request)
        {
            if (request == null) return;
            if (string.IsNullOrWhiteSpace(request.ModelName)) return;
            if (!request.Configuration.ContainsKey("Model") || string.IsNullOrWhiteSpace(request.Configuration["Model"]))
                request.Configuration["Model"] = request.ModelName;
        }

        public async Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                ApplyDefaults(request);
                ApplyModelNameToConfiguration(request);

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

        private void ApplyDefaults(EvaluationRequest request)
        {
            if (_options == null) return;

            if (string.IsNullOrWhiteSpace(request.Endpoint) && !string.IsNullOrWhiteSpace(_options.Endpoint))
                request.Endpoint = _options.Endpoint;

            if (request.PassThreshold <= 0 && _options.DefaultPassThreshold > 0)
                request.PassThreshold = _options.DefaultPassThreshold;

            foreach (var kv in _options.ToConfigurationDictionary())
            {
                if (!request.Configuration.ContainsKey(kv.Key))
                    request.Configuration[kv.Key] = kv.Value;
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
                    RiskLevel = "Low",
                    GroundednessScore = 1.0,
                    HallucinationRate = 0.0
                };
            }

            var unsupported = new List<string>();
            var partiallySupported = new List<string>();
            int supportedCount = 0;
            TokenUsage? usageAcc = null;

            const string systemInstruction = "You are a grounding validator. Given REFERENCE TEXT and one CLAIM from an AI response, output exactly one of: SUPPORTED, PARTIALLY_SUPPORTED, or UNSUPPORTED. Then optionally one line starting with Reason:";
            string refBlock = $"REFERENCE TEXT:\n{referenceText}";

            foreach (var statement in statements)
            {
                string prompt = $"{systemInstruction}\n\n{refBlock}\n\nCLAIM to check: \"{statement.Replace("\"", "'")}\"\n\nYour classification (one word):";
                string jsonResponse;
                try
                {
                    jsonResponse = await provider.GetResponseAsync(request.Endpoint, prompt, config, cancellationToken).ConfigureAwait(false);
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
                        RiskLevel = "High",
                        GroundednessScore = 0,
                        HallucinationRate = statements.Count == 0 ? 0 : (double)unsupported.Count / statements.Count,
                        Usage = usageAcc
                    };
                }

                usageAcc = TokenUsage.Combine(usageAcc, TokenUsageParser.TryParse(jsonResponse, config));

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
            double hallucinationRate = (double)unsupported.Count / statements.Count;
            string riskLevel = ComputeRiskLevel(supportedCount, partiallySupported.Count, unsupported.Count, statements.Count);
            bool isPassed = riskLevel != "High" && groundingScore >= request.PassThreshold;

            string details = $"Grounding: {supportedCount}/{statements.Count} statements fully supported ({groundingScore:P0}). Unsupported: {unsupported.Count}; Partial: {partiallySupported.Count}. Hallucination rate: {hallucinationRate:P0}. Risk: {riskLevel}.";

            return new EvaluationResult
            {
                Score = groundingScore,
                IsPassed = isPassed,
                Confidence = riskLevel == "Low" ? "High" : riskLevel == "Medium" ? "Medium" : "Low",
                Details = details,
                UnsupportedStatements = unsupported,
                PartiallySupportedStatements = partiallySupported,
                RiskLevel = riskLevel,
                GroundednessScore = groundingScore,
                HallucinationRate = hallucinationRate,
                Usage = usageAcc
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
                ProviderType.AzureOpenAI => LLMResponseParser.GetRawContentFromOpenAIResponse(jsonResponse),
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
                var usage = TokenUsageParser.TryParse(llmResponseJson, request.Configuration);

                LLMParseResult parsedResult;
                if (request.ProviderType == ProviderType.Gemini)
                {
                    parsedResult = LLMResponseParser.ParseGeminiEvaluationResponse(llmResponseJson);
                }
                else if (request.ProviderType == ProviderType.Ollama)
                {
                    parsedResult = LLMResponseParser.ParseOllamaEvaluationResponse(llmResponseJson);
                }
                else if (request.ProviderType == ProviderType.OpenAI || request.ProviderType == ProviderType.AzureOpenAI)
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
                    isPassed = false;
                    score = 0;
                }

                string confidence = score >= 0.8 ? "High" : score >= .5 ? "Medium" : "Low";

                return new EvaluationResult
                {
                    Score = score,
                    IsPassed = isPassed,
                    Details = parsedResult.Description ?? string.Empty,
                    Confidence = confidence,
                    Usage = usage
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
            var metricName = string.IsNullOrWhiteSpace(request.MatchingType) ? "exact" : request.MatchingType.Trim();
            if (!_metrics.TryGet(metricName, out var metric))
            {
                return new EvaluationResult
                {
                    Score = 0,
                    IsPassed = false,
                    MetricName = metricName,
                    Details = $"Unknown matching type / metric '{metricName}'. Registered: {string.Join(", ", _metrics.Names)}."
                };
            }

            var context = new MetricContext
            {
                Question = request.Question,
                Actual = request.AiResponse,
                Expected = request.GoldenOutput,
                Schema = request.Schema,
                PassThreshold = request.PassThreshold,
                Configuration = request.Configuration
            };

            var metricResult = await metric.EvaluateAsync(context).ConfigureAwait(false);
            return new EvaluationResult
            {
                Score = metricResult.Score,
                IsPassed = metricResult.IsPassed,
                Details = metricResult.Details,
                MetricName = metric.Name,
                GroundednessScore = string.Equals(metric.Name, "grounded-heuristic", StringComparison.OrdinalIgnoreCase)
                    ? metricResult.Score
                    : null,
                HallucinationRate = string.Equals(metric.Name, "grounded-heuristic", StringComparison.OrdinalIgnoreCase)
                    ? Math.Max(0, 1.0 - metricResult.Score)
                    : null
            };
        }
    }
}
