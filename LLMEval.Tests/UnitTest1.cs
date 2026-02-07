using LLMEval;

namespace LLMEval.Tests;

public class TfidfSimilarityTests
{
    [Fact]
    public void Calculate_IdenticalStrings_ReturnsHighScore()
    {
        var tfidf = new TfidfSimilarity();
        var (score, _) = tfidf.Calculate("hello world", "hello world");
        Assert.True(score > 0.99 && score <= 1.0);
    }

    [Fact]
    public void Calculate_SimilarStrings_ReturnsReasonableScore()
    {
        var tfidf = new TfidfSimilarity();
        var (score, _) = tfidf.Calculate("capital France Paris", "Paris capital France");
        Assert.True(score >= 0 && score <= 1.01, $"Expected score in [0,1], got {score}"); // Allow fp tolerance
    }

    [Fact]
    public void Calculate_UnrelatedStrings_ReturnsLowScore()
    {
        var tfidf = new TfidfSimilarity();
        var (score, _) = tfidf.Calculate("apple banana cherry", "dog cat bird");
        Assert.True(score < 0.5);
    }

    [Fact]
    public void Calculate_EmptyInput_ReturnsZero()
    {
        var tfidf = new TfidfSimilarity();
        var (score, reason) = tfidf.Calculate("", "something");
        Assert.Equal(0.0, score);
        Assert.Contains("No meaningful", reason);
    }
}

public class EvaluationServiceDirectTests
{
    [Fact]
    public async Task EvaluateAsync_DirectExactMatch_Passes()
    {
        var factory = new AiProviderFactory();
        var service = new AdvancedEvaluationService(factory);

        var request = new EvaluationRequest
        {
            Question = "Capital of France?",
            AiResponse = "Paris",
            GoldenOutput = "Paris",
            ProviderType = ProviderType.Gemini,
            Endpoint = "",
            Configuration = new Dictionary<string, string>(),
            MatchingType = "exact",
            PassThreshold = 0.9,
            EvaluationType = EvaluationType.DirectEvaluation,
            IsReferenceDoc = false
        };

        var result = await service.EvaluateAsync(request);

        Assert.Equal(1.0, result.Score);
        Assert.True(result.IsPassed);
    }

    [Fact]
    public async Task EvaluateAsync_DirectExactMatch_IgnoresCase()
    {
        var factory = new AiProviderFactory();
        var service = new AdvancedEvaluationService(factory);

        var request = new EvaluationRequest
        {
            Question = "Capital?",
            AiResponse = "PARIS",
            GoldenOutput = "paris",
            ProviderType = ProviderType.Gemini,
            Endpoint = "",
            Configuration = new Dictionary<string, string>(),
            MatchingType = "exact",
            PassThreshold = 0.9,
            EvaluationType = EvaluationType.DirectEvaluation,
            IsReferenceDoc = false
        };

        var result = await service.EvaluateAsync(request);

        Assert.Equal(1.0, result.Score);
        Assert.True(result.IsPassed);
    }

    [Fact]
    public async Task EvaluateAsync_DirectKeywordMatch_CalculatesCorrectly()
    {
        var factory = new AiProviderFactory();
        var service = new AdvancedEvaluationService(factory);

        var request = new EvaluationRequest
        {
            Question = "Capital?",
            AiResponse = "The capital is Paris in France",
            GoldenOutput = "Paris France",
            ProviderType = ProviderType.Gemini,
            Endpoint = "",
            Configuration = new Dictionary<string, string>(),
            MatchingType = "keyword",
            PassThreshold = 0.5,
            EvaluationType = EvaluationType.DirectEvaluation,
            IsReferenceDoc = false
        };

        var result = await service.EvaluateAsync(request);

        Assert.True(result.Score >= 0.5);
        Assert.True(result.IsPassed);
    }

    [Fact]
    public async Task EvaluateAsync_DirectSemantic_UsesTfidf()
    {
        var factory = new AiProviderFactory();
        var service = new AdvancedEvaluationService(factory);

        var request = new EvaluationRequest
        {
            Question = "Capital of France?",
            AiResponse = "Paris is the capital of France",
            GoldenOutput = "Paris",
            ProviderType = ProviderType.Gemini,
            Endpoint = "",
            Configuration = new Dictionary<string, string>(),
            MatchingType = "semantic",
            PassThreshold = 0.3,
            EvaluationType = EvaluationType.DirectEvaluation,
            IsReferenceDoc = false
        };

        var result = await service.EvaluateAsync(request);

        Assert.True(result.Score > 0);
        Assert.True(result.Score <= 1.0);
        Assert.False(string.IsNullOrEmpty(result.Details));
    }
}

public class AiProviderFactoryTests
{
    [Fact]
    public void CreateProvider_ReturnsCorrectProviderForEachType()
    {
        var factory = new AiProviderFactory();
        using var httpClient = new HttpClient();

        var ollama = factory.CreateProvider(ProviderType.Ollama, httpClient);
        Assert.IsType<OllamaProvider>(ollama);

        var openAi = factory.CreateProvider(ProviderType.OpenAI, httpClient);
        Assert.IsType<OpenAIProvider>(openAi);

        var gemini = factory.CreateProvider(ProviderType.Gemini, httpClient);
        Assert.IsType<GeminiProvider>(gemini);
    }

    [Fact]
    public void CreateProvider_ThrowsForUnsupportedType()
    {
        var factory = new AiProviderFactory();
        using var httpClient = new HttpClient();

        Assert.Throws<ArgumentException>(() =>
            factory.CreateProvider((ProviderType)999, httpClient));
    }
}

public class LLMResponseParserTests
{
    [Fact]
    public void ParseGeminiEvaluationResponse_ExtractsScoreAndDescription()
    {
        var json = @"{
            ""candidates"": [{
                ""content"": {
                    ""parts"": [{ ""text"": ""0.9 Paris is correct."" }]
                }
            }]
        }";

        var result = LLMResponseParser.ParseGeminiEvaluationResponse(json);

        Assert.Equal("0.9", result.ScoreString);
        Assert.Contains("Paris", result.Description);
    }

    [Fact]
    public void ParseOllamaEvaluationResponse_ExtractsScoreAndDescription()
    {
        var json = @"{
            ""response"": ""1.0 Exact match. Paris is correct."",
            ""model"": ""mistral"",
            ""done"": true
        }";

        var result = LLMResponseParser.ParseOllamaEvaluationResponse(json);

        Assert.Equal("1.0", result.ScoreString);
        Assert.Contains("Exact", result.Description);
    }

    [Fact]
    public void ParseOpenAIEvaluationResponse_ExtractsScoreAndDescription()
    {
        var json = @"{
            ""choices"": [{
                ""message"": {
                    ""content"": ""0.85 Paris is the correct answer."",
                    ""role"": ""assistant""
                }
            }]
        }";

        var result = LLMResponseParser.ParseOpenAIEvaluationResponse(json);

        Assert.Equal("0.85", result.ScoreString);
        Assert.Contains("Paris", result.Description);
    }

    [Fact]
    public void ParseLLMOutput_HandlesScoreAtEnd()
    {
        var json = @"{
            ""response"": ""The answer is correct. Score: 0.95"",
            ""done"": true
        }";

        var result = LLMResponseParser.ParseOllamaEvaluationResponse(json);

        Assert.Equal("0.95", result.ScoreString);
    }
}
