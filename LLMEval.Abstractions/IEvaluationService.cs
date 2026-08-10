namespace LLMEval;

/// <summary>Evaluates LLM outputs (direct metrics, LLM-as-judge, or groundedness).</summary>
public interface IEvaluationService
{
    Task<EvaluationResult> EvaluateAsync(EvaluationRequest request, CancellationToken cancellationToken = default);
}
