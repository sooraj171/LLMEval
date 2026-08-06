using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LLMEval;

/// <summary>DI registration helpers for STAF.LLMEval.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IAiProviderFactory"/>, <see cref="IEvaluationService"/>, named HttpClient, and options.</summary>
    public static IServiceCollection AddLLMEval(this IServiceCollection services, Action<LLMEvalOptions>? configure = null)
    {
        if (configure != null)
            services.Configure(configure);
        else
            services.AddOptions<LLMEvalOptions>();

        services.AddHttpClient("LLMEval");
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();
        services.AddSingleton<IEvaluationService>(sp =>
        {
            var factory = sp.GetRequiredService<IAiProviderFactory>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("LLMEval");
            var options = sp.GetService<IOptions<LLMEvalOptions>>()?.Value;
            return new AdvancedEvaluationService(factory, httpClient, options);
        });

        return services;
    }
}
