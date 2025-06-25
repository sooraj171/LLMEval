using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LLMEval
{
    public class TfidfSimilarity
    {
        private readonly HashSet<string> _stopWords = new() {
        "a", "an", "the", "is", "in", "at", "of", "on", "and", "or", "to", "for", "by", "with",
        "as", "be", "are", "was", "were", "been", "have", "has", "had", "do", "does", "did",
        "will", "would", "could", "should", "may", "might", "can", "must", "shall"
    };

        public (double, string) Calculate(string reference, string output)
        {
            var refTokens = Preprocess(reference);
            var outTokens = Preprocess(output);

            if (!refTokens.Any() || !outTokens.Any())
            {
                return (0.0, "No meaningful tokens found");
            }

            var allWords = refTokens.Union(outTokens).Distinct().ToList();
            var refVec = Vectorize(refTokens, allWords);
            var outVec = Vectorize(outTokens, allWords);

            double score = CosineSimilarity(refVec, outVec);
            int overlap = refTokens.Intersect(outTokens).Count();

            string reason = $"TF-IDF similarity with {overlap} overlapping terms. " +
                           $"Ref tokens: {refTokens.Count}, Out tokens: {outTokens.Count}";

            return (score, reason);
        }

        private List<string> Preprocess(string sentence)
        {
            return Regex.Split(sentence.ToLower(), @"\W+")
                        .Where(w => w.Length > 2 && !_stopWords.Contains(w))
                        .ToList();
        }

        private double[] Vectorize(List<string> tokens, List<string> vocabulary)
        {
            return vocabulary.Select(word => (double)tokens.Count(t => t == word)).ToArray();
        }

        private double CosineSimilarity(double[] v1, double[] v2)
        {
            double dot = v1.Zip(v2, (x, y) => x * y).Sum();
            double mag1 = Math.Sqrt(v1.Sum(x => x * x));
            double mag2 = Math.Sqrt(v2.Sum(y => y * y));
            return mag1 == 0 || mag2 == 0 ? 0 : dot / (mag1 * mag2);
        }
    }
}
