namespace LLMEval;

/// <summary>
/// Relevance of the response to the question via TF-IDF (MatchingType = relevance).
/// Expected is optional; when present, score is the average of question↔actual and expected↔actual similarity.
/// </summary>
public sealed class RelevanceMetric : IEvaluationMetric
{
    private readonly TfidfSimilarity _tfidf = new();

    public string Name => "relevance";

    public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        var question = context.Question ?? string.Empty;
        var actual = context.Actual ?? string.Empty;
        var expected = context.Expected ?? string.Empty;

        if (string.IsNullOrWhiteSpace(question) && string.IsNullOrWhiteSpace(expected))
        {
            return Task.FromResult(new MetricResult
            {
                Score = 0,
                IsPassed = false,
                Details = "Relevance requires a Question and/or Expected text."
            });
        }

        var (qScore, qDetails) = string.IsNullOrWhiteSpace(question)
            ? (0.0, "")
            : _tfidf.Calculate(question, actual);

        double score;
        string details;
        if (!string.IsNullOrWhiteSpace(expected) && !string.IsNullOrWhiteSpace(question))
        {
            var (eScore, _) = _tfidf.Calculate(expected, actual);
            score = (qScore + eScore) / 2.0;
            details = $"Relevance (avg question+expected TF-IDF): {score:0.###}. Q: {qDetails}";
        }
        else if (!string.IsNullOrWhiteSpace(question))
        {
            score = qScore;
            details = $"Relevance (question↔actual TF-IDF): {score:0.###}. {qDetails}";
        }
        else
        {
            var (eScore, eDetails) = _tfidf.Calculate(expected, actual);
            score = eScore;
            details = $"Relevance (expected↔actual TF-IDF): {score:0.###}. {eDetails}";
        }

        return Task.FromResult(new MetricResult
        {
            Score = score,
            IsPassed = score >= context.PassThreshold,
            Details = details
        });
    }
}

/// <summary>
/// Lightweight heuristic groundedness without an LLM (MatchingType = grounded-heuristic).
/// Splits Actual into statements and scores how many have substantial token overlap with the reference (Expected / Schema unused).
/// </summary>
public sealed class HeuristicGroundingMetric : IEvaluationMetric
{
    public string Name => "grounded-heuristic";

    public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        var reference = context.Expected ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Task.FromResult(new MetricResult
            {
                Score = 0,
                IsPassed = false,
                Details = "Heuristic grounding requires Expected (reference) text."
            });
        }

        var statements = ResponseStatementSplitter.SplitIntoStatements(context.Actual ?? string.Empty);
        if (statements.Count == 0)
        {
            return Task.FromResult(new MetricResult
            {
                Score = 1.0,
                IsPassed = true,
                Details = "No factual statements to check; treated as grounded."
            });
        }

        var refTokens = Tokenize(reference);
        var supported = 0;
        var unsupported = new List<string>();

        foreach (var statement in statements)
        {
            var claimTokens = Tokenize(statement);
            if (claimTokens.Count == 0)
            {
                supported++;
                continue;
            }

            var overlap = claimTokens.Count(t => refTokens.Contains(t));
            var ratio = (double)overlap / claimTokens.Count;
            if (ratio >= 0.4)
                supported++;
            else
                unsupported.Add(statement);
        }

        var score = (double)supported / statements.Count;
        return Task.FromResult(new MetricResult
        {
            Score = score,
            IsPassed = score >= context.PassThreshold && unsupported.Count == 0,
            Details = unsupported.Count == 0
                ? $"Heuristic grounding: {supported}/{statements.Count} statements overlap reference."
                : $"Heuristic grounding: {supported}/{statements.Count} supported. Unsupported: {string.Join(" | ", unsupported.Take(3))}"
        });
    }

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '{', '}' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToHashSet(StringComparer.Ordinal);
}
