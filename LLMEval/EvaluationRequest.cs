namespace LLMEval
{
    public enum ProviderType
    {
        Ollama,
        OpenAI,
        Gemini
    }
    public enum EvaluationType
    {
        LLMAsJudge,
        DirectEvaluation
    }

    public class EvaluationRequest
    {
        public string Question { get; set; }
        public string AiResponse { get; set; }
        public string GoldenOutput { get; set; }
        public ProviderType ProviderType { get; set; }
        public string Endpoint { get; set; }
        public Dictionary<string, string> Configuration { get; set; }
        public string? MatchingType { get; set; } // e.g., "exact", "keyword", "semantic"
        public double PassThreshold { get; set; } // User-configurable pass threshold
        public string? ModelName { get; set; } // Name of the model being evaluated
        public EvaluationType EvaluationType { get; set; } 
        public bool IsReferenceDoc { get; set; }
    }
}




