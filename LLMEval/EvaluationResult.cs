
namespace LLMEval
{
    public class EvaluationResult
    {
        public double Score { get; set; }
        public bool IsPassed { get; set; }
        public string Confidence { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}
