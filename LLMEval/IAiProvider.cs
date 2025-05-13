
namespace LLMEval
{
    public interface IAiProvider
    {
        Task<string> GetResponseAsync(string endPonint,string prompt, Dictionary<string, string> configuration);
    }
}
