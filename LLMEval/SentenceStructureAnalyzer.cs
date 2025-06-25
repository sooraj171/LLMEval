using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LLMEval
{
    public class SentenceStructureAnalyzer
    {
        public double CalculateStructuralSimilarity(string reference, string output)
        {
            var refStructure = AnalyzeStructure(reference);
            var outStructure = AnalyzeStructure(output);

            double sentenceCountSim = 1.0 - Math.Abs(refStructure.SentenceCount - outStructure.SentenceCount) * 0.1;
            double avgLengthSim = 1.0 - Math.Abs(refStructure.AvgSentenceLength - outStructure.AvgSentenceLength) * 0.01;
            double punctuationSim = CalculatePunctuationSimilarity(refStructure.PunctuationPattern, outStructure.PunctuationPattern);

            return (sentenceCountSim + avgLengthSim + punctuationSim) / 3.0;
        }

        private (int SentenceCount, double AvgSentenceLength, string PunctuationPattern) AnalyzeStructure(string text)
        {
            var sentences = Regex.Split(text, @"[.!?]+")
                                 .Where(s => !string.IsNullOrWhiteSpace(s))
                                 .ToArray();

            int sentenceCount = sentences.Length;
            double avgLength = sentences.Any() ? sentences.Average(s => s.Trim().Length) : 0;
            string punctPattern = Regex.Replace(text, @"[^.!?,:;]", "");

            return (sentenceCount, avgLength, punctPattern);
        }

        private double CalculatePunctuationSimilarity(string pattern1, string pattern2)
        {
            if (string.IsNullOrEmpty(pattern1) && string.IsNullOrEmpty(pattern2)) return 1.0;
            if (string.IsNullOrEmpty(pattern1) || string.IsNullOrEmpty(pattern2)) return 0.0;

            int maxLen = Math.Max(pattern1.Length, pattern2.Length);
            int minLen = Math.Min(pattern1.Length, pattern2.Length);

            int matches = 0;
            for (int i = 0; i < minLen; i++)
            {
                if (pattern1[i] == pattern2[i]) matches++;
            }

            return (double)matches / maxLen;
        }
    }

}
