using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LLMEval
{
    public class GloveModel
    {
        private readonly Dictionary<string, float[]> _embeddings = new();
        private readonly Dictionary<string, int> _wordFrequency = new();
        private readonly int _vectorSize;

        public GloveModel(string glovePath, int vectorSize)
        {
            _vectorSize = vectorSize;
            LoadEmbeddings(glovePath);
        }

        private void LoadEmbeddings(string glovePath)
        {
            foreach (var line in File.ReadLines(glovePath))
            {
                var parts = line.Split(' ');
                if (parts.Length != _vectorSize + 1) continue;

                var word = parts[0];
                var vector = parts.Skip(1).Select(float.Parse).ToArray();
                _embeddings[word] = vector;

                // Track word frequency (you could load this from a separate corpus)
                _wordFrequency[word] = _wordFrequency.GetValueOrDefault(word, 0) + 1;
            }
        }

        public float[] GetSentenceVector(string sentence)
        {
            var tokens = Preprocess(sentence);
            var vectors = new List<(float[] vector, double weight)>();

            foreach (var token in tokens)
            {
                if (_embeddings.ContainsKey(token))
                {
                    double weight = CalculateWordWeight(token, tokens.Count);
                    vectors.Add((_embeddings[token], weight));
                }
            }

            if (!vectors.Any()) return new float[_vectorSize];

            // Weighted average instead of simple average
            var result = new float[_vectorSize];
            double totalWeight = 0;

            foreach (var (vector, weight) in vectors)
            {
                for (int i = 0; i < _vectorSize; i++)
                    result[i] += (float)(vector[i] * weight);
                totalWeight += weight;
            }

            if (totalWeight > 0)
            {
                for (int i = 0; i < _vectorSize; i++)
                    result[i] /= (float)totalWeight;
            }

            return result;
        }

        private double CalculateWordWeight(string word, int sentenceLength)
        {
            // TF-IDF inspired weighting
            double tf = 1.0 / sentenceLength; // Simple term frequency
            int frequency = _wordFrequency.GetValueOrDefault(word, 1);
            double idf = Math.Log((double)_embeddings.Count / frequency); // Inverse document frequency
            return tf * idf;
        }

        public bool IsRareWord(string word)
        {
            int frequency = _wordFrequency.GetValueOrDefault(word, 0);
            return frequency < 100; // Threshold for "rare" words
        }

        public double CosineSimilarity(float[] v1, float[] v2)
        {
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < v1.Length; i++)
            {
                dot += v1[i] * v2[i];
                normA += v1[i] * v1[i];
                normB += v2[i] * v2[i];
            }

            double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
            return denominator > 1e-10 ? dot / denominator : 0;
        }

        public List<string> Preprocess(string sentence)
        {
            // Enhanced preprocessing
            sentence = sentence.ToLower();

            // Handle contractions
            sentence = sentence.Replace("'s", " is")
                              .Replace("'re", " are")
                              .Replace("'ve", " have")
                              .Replace("n't", " not");

            // Extract meaningful tokens
            return Regex.Split(sentence, @"\W+")
                       .Where(w => w.Length > 2)
                       .ToList();
        }
    }

}
