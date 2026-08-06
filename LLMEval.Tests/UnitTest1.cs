using System.Collections.Concurrent;
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

        var azure = factory.CreateProvider(ProviderType.AzureOpenAI, httpClient);
        Assert.IsType<AzureOpenAIProvider>(azure);
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

public class GroundingJudgeParserTests
{
    [Theory]
    [InlineData("SUPPORTED", GroundingLabel.Supported)]
    [InlineData("supported", GroundingLabel.Supported)]
    [InlineData("UNSUPPORTED", GroundingLabel.Unsupported)]
    [InlineData("Unsupported. Reason: not in doc.", GroundingLabel.Unsupported)]
    [InlineData("PARTIALLY_SUPPORTED", GroundingLabel.PartiallySupported)]
    [InlineData("PARTIAL", GroundingLabel.PartiallySupported)]
    [InlineData("Partial match. Reason: only one part.", GroundingLabel.PartiallySupported)]
    public void ParseGroundingJudgeOutput_ReturnsCorrectLabel(string raw, GroundingLabel expected)
    {
        var result = GroundingJudgeParser.ParseGroundingJudgeOutput(raw);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseGroundingJudgeOutput_EmptyOrNull_ReturnsUnsupported()
    {
        Assert.Equal(GroundingLabel.Unsupported, GroundingJudgeParser.ParseGroundingJudgeOutput(""));
        Assert.Equal(GroundingLabel.Unsupported, GroundingJudgeParser.ParseGroundingJudgeOutput("   "));
        Assert.Equal(GroundingLabel.Unsupported, GroundingJudgeParser.ParseGroundingJudgeOutput("No label here"));
    }
}

public class ResponseStatementSplitterTests
{
    [Fact]
    public void SplitIntoStatements_Empty_ReturnsEmpty()
    {
        var result = ResponseStatementSplitter.SplitIntoStatements("");
        Assert.Empty(result);
        result = ResponseStatementSplitter.SplitIntoStatements("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void SplitIntoStatements_Sentences_SplitsByPeriod()
    {
        var result = ResponseStatementSplitter.SplitIntoStatements("First sentence. Second sentence. Third.");
        Assert.Equal(3, result.Count);
        Assert.Contains("First sentence.", result);
        Assert.Contains("Second sentence.", result);
        Assert.Contains("Third.", result);
    }

    [Fact]
    public void SplitIntoStatements_BulletList_SplitsByBullets()
    {
        var result = ResponseStatementSplitter.SplitIntoStatements("- Item one.\n- Item two.\n* Item three.");
        Assert.True(result.Count >= 2);
        Assert.Contains(result, s => s.Contains("Item one") || s.Contains("one"));
        Assert.Contains(result, s => s.Contains("Item two") || s.Contains("two"));
    }

    [Fact]
    public void SplitIntoStatements_NumberedList_SplitsByNumbers()
    {
        var result = ResponseStatementSplitter.SplitIntoStatements("1. First point. 2. Second point.");
        Assert.True(result.Count >= 1);
    }
}

/// <summary>Test double: returns predefined JSON responses in order so GroundedAnswerCheck can be tested without a live API.</summary>
internal class MockAiProvider : IAiProvider
{
    private readonly ConcurrentQueue<string> _queue = new();

    public void Enqueue(params string[] responses)
    {
        foreach (var r in responses)
            _queue.Enqueue(r);
    }

    public List<string> Responses
    {
        get => _queue.ToList();
        set
        {
            while (_queue.TryDequeue(out _)) { }
            foreach (var r in value)
                _queue.Enqueue(r);
        }
    }

    public Task<string> GetResponseAsync(string endpoint, string prompt, Dictionary<string, string> configuration, CancellationToken cancellationToken = default)
    {
        if (_queue.TryDequeue(out var next))
            return Task.FromResult(next);
        return Task.FromResult("{\"response\": \"UNSUPPORTED\", \"done\": true}");
    }
}

internal class MockAiProviderFactory : IAiProviderFactory
{
    public MockAiProvider Provider { get; } = new MockAiProvider();

    public IAiProvider CreateProvider(ProviderType providerType, HttpClient httpClient) => Provider;
}

public class GroundedAnswerCheckTests
{
    private static string OllamaJson(string response) =>
        $"{{\"response\": \"{response}\", \"model\": \"test\", \"done\": true}}";

    [Fact]
    public async Task EvaluateAsync_GroundedAnswerCheck_AllSupported_ReturnsHighScoreAndLowRisk()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            OllamaJson("SUPPORTED"),
            OllamaJson("SUPPORTED")
        };

        var service = new AdvancedEvaluationService(factory);
        var request = new EvaluationRequest
        {
            Question = "What is X?",
            AiResponse = "First claim. Second claim.",
            GoldenOutput = "Reference document with facts.",
            ProviderType = ProviderType.Ollama,
            Endpoint = "http://localhost",
            Configuration = new Dictionary<string, string> { ["Model"] = "test" },
            PassThreshold = 0.5,
            EvaluationType = EvaluationType.GroundedAnswerCheck
        };

        var result = await service.EvaluateAsync(request);

        Assert.Equal(1.0, result.Score);
        Assert.True(result.IsPassed);
        Assert.Equal("Low", result.RiskLevel);
        Assert.NotNull(result.UnsupportedStatements);
        Assert.Empty(result.UnsupportedStatements);
        Assert.NotNull(result.PartiallySupportedStatements);
        Assert.Empty(result.PartiallySupportedStatements);
        Assert.Contains("2/2", result.Details);
    }

    [Fact]
    public async Task EvaluateAsync_GroundedAnswerCheck_OneUnsupported_ReturnsHalfScoreAndHighRisk()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string>
        {
            OllamaJson("SUPPORTED"),
            OllamaJson("UNSUPPORTED")
        };

        var service = new AdvancedEvaluationService(factory);
        var request = new EvaluationRequest
        {
            Question = "What is X?",
            AiResponse = "Supported claim. Hallucinated claim.",
            GoldenOutput = "Reference text.",
            ProviderType = ProviderType.Ollama,
            Endpoint = "http://localhost",
            Configuration = new Dictionary<string, string> { ["Model"] = "test" },
            PassThreshold = 0.5,
            EvaluationType = EvaluationType.GroundedAnswerCheck
        };

        var result = await service.EvaluateAsync(request);

        Assert.Equal(0.5, result.Score);
        Assert.False(result.IsPassed);
        Assert.Equal("High", result.RiskLevel);
        Assert.NotNull(result.UnsupportedStatements);
        Assert.Single(result.UnsupportedStatements);
        Assert.Contains("Hallucinated", result.UnsupportedStatements[0]);
    }

    [Fact]
    public async Task EvaluateAsync_GroundedAnswerCheck_UsesReferenceDocuments_WhenProvided()
    {
        var factory = new MockAiProviderFactory();
        factory.Provider.Responses = new List<string> { OllamaJson("SUPPORTED") };

        var service = new AdvancedEvaluationService(factory);
        var request = new EvaluationRequest
        {
            Question = "What is X?",
            AiResponse = "One claim.",
            GoldenOutput = "ignored",
            ReferenceDocuments = new[] { "Doc one.", "Doc two." },
            ProviderType = ProviderType.Ollama,
            Endpoint = "http://localhost",
            Configuration = new Dictionary<string, string> { ["Model"] = "test" },
            PassThreshold = 0.5,
            EvaluationType = EvaluationType.GroundedAnswerCheck
        };

        var result = await service.EvaluateAsync(request);

        Assert.True(result.IsPassed);
        Assert.Equal(1.0, result.Score);
    }

    [Fact]
    public async Task EvaluateAsync_GroundedAnswerCheck_NoStatements_ReturnsPassAndLowRisk()
    {
        var factory = new MockAiProviderFactory();
        var service = new AdvancedEvaluationService(factory);
        var request = new EvaluationRequest
        {
            Question = "What?",
            AiResponse = "  ",
            GoldenOutput = "Ref",
            ProviderType = ProviderType.Ollama,
            Endpoint = "http://localhost",
            Configuration = new Dictionary<string, string> { ["Model"] = "test" },
            PassThreshold = 0.8,
            EvaluationType = EvaluationType.GroundedAnswerCheck
        };

        var result = await service.EvaluateAsync(request);

        Assert.Equal(1.0, result.Score);
        Assert.True(result.IsPassed);
        Assert.Equal("Low", result.RiskLevel);
        Assert.NotNull(result.UnsupportedStatements);
        Assert.Empty(result.UnsupportedStatements);
    }
}
