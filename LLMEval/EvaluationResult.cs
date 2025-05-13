
namespace LLMEval
{
    public class EvaluationResult
    {
        public double Score { get; set; }
        public bool IsPassed { get; set; }
        public double? Confidence { get; set; } // Nullable as you don't have specific requirements yet
        public string? Details { get; set; }
    }
}
