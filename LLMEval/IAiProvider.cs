
namespace LLMEval
{
    public interface IAiProvider
    {
        Task<string> GetResponseAsync(string endpoint, string prompt, Dictionary<string, string> configuration, CancellationToken cancellationToken = default);
    }
}
