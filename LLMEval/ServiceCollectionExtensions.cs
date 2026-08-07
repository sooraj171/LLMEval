using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LLMEval;

/// <summary>DI registration helpers for STAF.LLMEval.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAiProviderFactory"/>, <see cref="IEvaluationService"/>, named HttpClient,
    /// <see cref="MetricRegistry"/>, and options. Optional <paramref name="configureMetrics"/> registers custom metrics.
    /// </summary>
    public static IServiceCollection AddLLMEval(
        this IServiceCollection services,
        Action<LLMEvalOptions>? configure = null,
        Action<MetricRegistry>? configureMetrics = null)
    {
        if (configure != null)
            services.Configure(configure);
        else
            services.AddOptions<LLMEvalOptions>();

        services.AddHttpClient("LLMEval");
        services.AddSingleton<IAiProviderFactory, AiProviderFactory>();
        services.AddSingleton(sp =>
        {
            var registry = MetricRegistry.CreateDefault();
            foreach (var metric in sp.GetServices<IEvaluationMetric>())
                registry.Register(metric);
            configureMetrics?.Invoke(registry);
            return registry;
        });
        services.AddSingleton<IEvaluationService>(sp =>
        {
            var factory = sp.GetRequiredService<IAiProviderFactory>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("LLMEval");
            var options = sp.GetService<IOptions<LLMEvalOptions>>()?.Value;
            var metrics = sp.GetRequiredService<MetricRegistry>();
            return new AdvancedEvaluationService(factory, httpClient, options, metrics);
        });

        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IEvaluationMetric"/> for discovery by <see cref="AddLLMEval"/>.
    /// Call before or with <see cref="AddLLMEval"/>; metrics are applied when the registry is constructed.
    /// </summary>
    public static IServiceCollection AddLLMEvalMetric<TMetric>(this IServiceCollection services)
        where TMetric : class, IEvaluationMetric
    {
        services.AddSingleton<IEvaluationMetric, TMetric>();
        return services;
    }
}
