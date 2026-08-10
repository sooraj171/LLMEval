namespace LLMEval;

/// <summary>Evaluation strategy for an <see cref="EvaluationRequest"/>.</summary>
public enum EvaluationType
{
    LLMAsJudge,
    DirectEvaluation,
    /// <summary>Hallucination &amp; grounding validation: checks each factual statement in the AI response against reference document(s).</summary>
    GroundedAnswerCheck
}
