using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LLMEval
{
    public class SemanticSimilarityEvaluator
    {
        private readonly GloveModel _gloveModel;
        private readonly TfidfSimilarity _tfidf;
        private readonly SentenceStructureAnalyzer _structureAnalyzer;

        public SemanticSimilarityEvaluator(string gloveFilePath, int vectorSize)
        {
            _gloveModel = new GloveModel(gloveFilePath, vectorSize);
            _tfidf = new TfidfSimilarity();
            _structureAnalyzer = new SentenceStructureAnalyzer();
        }

        public SimilarityResult Evaluate(string reference, string output)
        {
            // Early validation
            var validationResult = ValidateInputs(reference, output);
            if (validationResult != null) return validationResult;

            // Preprocess both texts
            var refTokens = _gloveModel.Preprocess(reference);
            var outTokens = _gloveModel.Preprocess(output);

            // Get embeddings
            var refVec = _gloveModel.GetSentenceVector(reference);
            var outVec = _gloveModel.GetSentenceVector(output);

            bool useGlove = refVec.Length > 0 && outVec.Length > 0 &&
                           !IsZeroVector(refVec) && !IsZeroVector(outVec);

            if (useGlove)
            {
                return CalculateGloveBasedSimilarity(reference, output, refVec, outVec, refTokens, outTokens);
            }
            else
            {
                var (score, reason) = _tfidf.Calculate(reference, output);
                return new SimilarityResult(score, "TF-IDF", reason, new Dictionary<string, double>());
            }
        }

        private SimilarityResult? ValidateInputs(string reference, string output)
        {
            if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(output))
            {
                return new SimilarityResult(0.0, "Validation", "Empty input detected.", new Dictionary<string, double>());
            }

            var outputTokens = _gloveModel.Preprocess(output);
            if (outputTokens.Count < 3) // Reduced threshold
            {
                return new SimilarityResult(0.0, "OutputTooShort", "Output has too few meaningful tokens.", new Dictionary<string, double>());
            }

            // Enhanced error pattern detection
            var errorPatterns = new[]
            {
            @"(api error|could not|get response|null|failed|timeout)",
            @"(error|exception|stack trace)",
            @"(unable to|cannot process|invalid)",
            @"(503|404|500|connection refused)"
        };

            foreach (var pattern in errorPatterns)
            {
                if (Regex.IsMatch(output.ToLower(), pattern))
                {
                    return new SimilarityResult(0.0, "ErrorPattern", $"Detected error pattern: {pattern}", new Dictionary<string, double>());
                }
            }

            return null;
        }

        private SimilarityResult CalculateGloveBasedSimilarity(string reference, string output,
            float[] refVec, float[] outVec, List<string> refTokens, List<string> outTokens)
        {
            var components = new Dictionary<string, double>();

            // 1. Base cosine similarity
            double baseSimilarity = _gloveModel.CosineSimilarity(refVec, outVec);
            components["BaseSimilarity"] = baseSimilarity;

            // 2. Enhanced keyword overlap with importance weighting
            var keywordAnalysis = AnalyzeKeywordOverlap(refTokens, outTokens);
            components["KeywordOverlapRatio"] = keywordAnalysis.overlapRatio;
            components["ImportantWordsRatio"] = keywordAnalysis.importantWordsRatio;

            // 3. Length-based adjustment (improved)
            double lengthRatio = Math.Min((double)outTokens.Count / refTokens.Count, 2.0); // Cap at 2x
            double lengthAdjustment = CalculateLengthAdjustment(lengthRatio);
            components["LengthAdjustment"] = lengthAdjustment;

            // 4. Structural similarity
            double structuralSimilarity = _structureAnalyzer.CalculateStructuralSimilarity(reference, output);
            components["StructuralSimilarity"] = structuralSimilarity;

            // 5. Content coverage (how much of the reference is covered)
            double coverage = CalculateContentCoverage(refTokens, outTokens);
            components["ContentCoverage"] = coverage;

            // Weighted combination
            double finalScore = CombineScores(baseSimilarity, keywordAnalysis, lengthAdjustment,
                                            structuralSimilarity, coverage);
            components["FinalScore"] = finalScore;

            string reason = $"GloVe-based evaluation: Base={baseSimilarity:F3}, Keywords={keywordAnalysis.overlapRatio:F3}, " +
                           $"Length={lengthAdjustment:F3}, Structure={structuralSimilarity:F3}, Coverage={coverage:F3}";

            return new SimilarityResult(Math.Min(finalScore, 1.0), "Enhanced-GloVe", reason, components);
        }

        private (double overlapRatio, double importantWordsRatio) AnalyzeKeywordOverlap(List<string> refTokens, List<string> outTokens)
        {
            var refSet = new HashSet<string>(refTokens);
            var outSet = new HashSet<string>(outTokens);
            var overlap = refSet.Intersect(outSet).ToList();

            double overlapRatio = (double)overlap.Count / Math.Max(refSet.Count, 1);

            // Identify important words (longer words, proper nouns, domain-specific terms)
            var importantRefWords = refTokens.Where(IsImportantWord).ToHashSet();
            var importantOverlap = overlap.Where(IsImportantWord).Count();
            double importantWordsRatio = importantRefWords.Count > 0 ?
                (double)importantOverlap / importantRefWords.Count : 0;

            return (overlapRatio, importantWordsRatio);
        }

        private bool IsImportantWord(string word)
        {
            return word.Length > 4 || // Longer words
                   char.IsUpper(word[0]) || // Potential proper nouns
                   _gloveModel.IsRareWord(word); // Less common words
        }

        private double CalculateLengthAdjustment(double lengthRatio)
        {
            if (lengthRatio < 0.1) return 0.3; // Very short outputs
            if (lengthRatio < 0.3) return 0.6;
            if (lengthRatio < 0.7) return 0.8;
            if (lengthRatio <= 1.3) return 1.0; // Sweet spot
            if (lengthRatio <= 2.0) return 0.9;
            return 0.7; // Very long outputs
        }

        private double CalculateContentCoverage(List<string> refTokens, List<string> outTokens)
        {
            if (!refTokens.Any()) return 0;

            // Calculate how many reference concepts are covered
            var refConcepts = ExtractConcepts(refTokens);
            var outConcepts = ExtractConcepts(outTokens);

            var coveredConcepts = refConcepts.Intersect(outConcepts, StringComparer.OrdinalIgnoreCase).Count();
            return (double)coveredConcepts / refConcepts.Count;
        }

        private List<string> ExtractConcepts(List<string> tokens)
        {
            // Extract meaningful concepts - could be enhanced with NER
            return tokens.Where(t => t.Length > 3 && !IsCommonWord(t)).ToList();
        }

        private bool IsCommonWord(string word)
        {
            var common = new HashSet<string> { "that", "this", "with", "from", "they", "were", "been",
                                         "have", "their", "said", "each", "which", "time", "will" };
            return common.Contains(word.ToLower());
        }

        private double CombineScores(double baseSimilarity, (double overlapRatio, double importantWordsRatio) keywords,
                                   double lengthAdjustment, double structuralSimilarity, double coverage)
        {
            // Weighted combination - adjust weights based on your needs
            double score = baseSimilarity * 0.35 +           // Core semantic similarity
                          keywords.overlapRatio * 0.25 +      // Keyword overlap
                          keywords.importantWordsRatio * 0.15 + // Important word overlap
                          structuralSimilarity * 0.1 +        // Structural similarity
                          coverage * 0.15;                     // Content coverage

            return score * lengthAdjustment; // Apply length adjustment
        }

        private bool IsZeroVector(float[] vector) => vector.All(v => Math.Abs(v) < 1e-10);
    }


}
