namespace LLMEval;

/// <summary>
/// Thrown when an evaluation cannot run because required configuration is missing.
/// This is not a scored failure: the metric did not execute.
/// </summary>
public class LLMEvalConfigurationException : Exception
{
    public LLMEvalConfigurationException(string message)
        : base(message)
    {
    }

    public LLMEvalConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
