namespace LLMEval
{
    public class EvaluationResult
    {
        public double Score { get; set; }
        public bool IsPassed { get; set; }
        public string Confidence { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;

        /// <summary>Name of the DirectEvaluation metric used (e.g. exact, semantic, schema), when applicable.</summary>
        public string? MetricName { get; set; }

        /// <summary>When <see cref="EvaluationType.GroundedAnswerCheck"/>: statements classified as unsupported (hallucinations).</summary>
        public IReadOnlyList<string>? UnsupportedStatements { get; set; }

        /// <summary>When <see cref="EvaluationType.GroundedAnswerCheck"/>: statements only partially supported by the reference.</summary>
        public IReadOnlyList<string>? PartiallySupportedStatements { get; set; }

        /// <summary>When <see cref="EvaluationType.GroundedAnswerCheck"/>: overall risk level — Low, Medium, or High.</summary>
        public string? RiskLevel { get; set; }

        /// <summary>
        /// Fraction of statements fully supported (same as Score for grounding). Alias for clarity in reports.
        /// </summary>
        public double? GroundednessScore { get; set; }

        /// <summary>
        /// Fraction of statements classified as unsupported (hallucination rate). Null when not a grounding eval.
        /// </summary>
        public double? HallucinationRate { get; set; }

        /// <summary>Best-effort token/cost usage when the provider response included usage metadata.</summary>
        public TokenUsage? Usage { get; set; }
    }
}
