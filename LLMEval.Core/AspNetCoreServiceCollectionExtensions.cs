using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LLMEval;

/// <summary>
/// ASP.NET Core / generic-host helpers. Aspire and host apps can bind the <c>LLMEval</c> configuration section
/// without a separate hosting package.
/// </summary>
public static class AspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers LLMEval and binds <see cref="LLMEvalOptions"/> from <paramref name="configuration"/>
    /// (section <see cref="LLMEvalOptions.SectionName"/> = <c>LLMEval</c>).
    /// </summary>
    public static IServiceCollection AddLLMEval(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<LLMEvalOptions>? configure = null,
        Action<MetricRegistry>? configureMetrics = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<LLMEvalOptions>(configuration.GetSection(LLMEvalOptions.SectionName));
        return services.AddLLMEval(configure, configureMetrics);
    }
}
