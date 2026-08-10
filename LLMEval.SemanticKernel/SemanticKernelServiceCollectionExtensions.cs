using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LLMEval.Integrations.SemanticKernel;

/// <summary>DI helpers for STAF.LLMEval + Semantic Kernel.</summary>
public static class SemanticKernelServiceCollectionExtensions
{
    /// <summary>
    /// Registers STAF.LLMEval and replaces <see cref="IAiProviderFactory"/> with
    /// <see cref="SemanticKernelProviderFactory"/> using the DI <see cref="IChatCompletionService"/>.
    /// Call after registering Semantic Kernel chat completion.
    /// </summary>
    public static IServiceCollection AddLLMEvalSemanticKernel(
        this IServiceCollection services,
        Action<LLMEvalOptions>? configure = null,
        Action<MetricRegistry>? configureMetrics = null)
    {
        services.AddLLMEval(configure, configureMetrics);
        services.AddSingleton<IAiProviderFactory>(sp =>
        {
            var chat = sp.GetService<IChatCompletionService>()
                       ?? sp.GetService<Kernel>()?.GetRequiredService<IChatCompletionService>()
                       ?? throw new InvalidOperationException(
                           "Register IChatCompletionService or Kernel before AddLLMEvalSemanticKernel.");
            return new SemanticKernelProviderFactory(chat);
        });
        return services;
    }
}
