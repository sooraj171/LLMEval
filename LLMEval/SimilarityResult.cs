using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMEval
{
    public class SimilarityResult
    {
        public double Score { get; }
        public string EngineUsed { get; }
        public string Reason { get; }
        public Dictionary<string, double> ComponentScores { get; }

        public SimilarityResult(double score, string engineUsed, string reason, Dictionary<string, double> componentScores)
        {
            Score = score;
            EngineUsed = engineUsed;
            Reason = reason;
            ComponentScores = componentScores ?? new Dictionary<string, double>();
        }

        public override string ToString()
        {
            var components = ComponentScores.Any()
                ? string.Join(", ", ComponentScores.Select(kv => $"{kv.Key}={kv.Value:F3}"))
                : "No components";
            return $"Score: {Score:F4}, Engine: {EngineUsed}, Components: [{components}]";
        }
    }
}
