namespace LLMEval
{
    public class EvaluationResult
    {
        public double Score { get; set; }
        public bool IsPassed { get; set; }
        public string Confidence { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;

        /// <summary>When <see cref="EvaluationType.GroundedAnswerCheck"/>: statements classified as unsupported (hallucinations).</summary>
        public IReadOnlyList<string>? UnsupportedStatements { get; set; }

        /// <summary>When <see cref="EvaluationType.GroundedAnswerCheck"/>: statements only partially supported by the reference.</summary>
        public IReadOnlyList<string>? PartiallySupportedStatements { get; set; }

        /// <summary>When <see cref="EvaluationType.GroundedAnswerCheck"/>: overall risk level — Low, Medium, or High.</summary>
        public string? RiskLevel { get; set; }
    }
}
