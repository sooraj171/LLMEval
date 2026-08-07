using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LLMEval.Integrations.SemanticKernel;

/// <summary>
/// <see cref="IAiProvider"/> backed by Semantic Kernel <see cref="IChatCompletionService"/>.
/// Returns an OpenAI-shaped JSON envelope so existing judge/grounding parsers work unchanged.
/// </summary>
public sealed class SemanticKernelChatProvider : IAiProvider
{
    private readonly IChatCompletionService _chat;

    public SemanticKernelChatProvider(IChatCompletionService chatCompletion)
    {
        _chat = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
    }

    /// <summary>Creates a provider from a <see cref="Kernel"/> that has a chat completion service registered.</summary>
    public SemanticKernelChatProvider(Kernel kernel)
        : this(kernel.GetRequiredService<IChatCompletionService>())
    {
    }

    public async Task<string> GetResponseAsync(
        string endpoint,
        string prompt,
        Dictionary<string, string> configuration,
        CancellationToken cancellationToken = default)
    {
        _ = endpoint; // SK uses the registered service endpoint, not EvaluationRequest.Endpoint

        PromptExecutionSettings? settings = null;
        if (configuration.TryGetValue("Temperature", out var tempStr)
            && double.TryParse(tempStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t))
        {
            settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object> { ["temperature"] = Math.Clamp(t, 0, 2) }
            };
        }

        var message = await _chat.GetChatMessageContentAsync(
            prompt,
            settings,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var text = message.Content ?? string.Empty;
        return JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content = text } }
            }
        });
    }
}

/// <summary>
/// <see cref="IAiProviderFactory"/> that always returns <see cref="SemanticKernelChatProvider"/>
/// (ProviderType is ignored — the Kernel/service selects the model).
/// </summary>
public sealed class SemanticKernelProviderFactory : IAiProviderFactory
{
    private readonly IChatCompletionService _chat;

    public SemanticKernelProviderFactory(IChatCompletionService chatCompletion)
    {
        _chat = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
    }

    public SemanticKernelProviderFactory(Kernel kernel)
        : this(kernel.GetRequiredService<IChatCompletionService>())
    {
    }

    public IAiProvider CreateProvider(ProviderType providerType, HttpClient httpClient)
    {
        _ = providerType;
        _ = httpClient;
        return new SemanticKernelChatProvider(_chat);
    }
}
