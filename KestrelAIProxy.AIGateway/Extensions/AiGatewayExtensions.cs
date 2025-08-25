using KestrelAIProxy.AIGateway.Core;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace KestrelAIProxy.AIGateway.Extensions;

public static class AiGatewayExtensions
{
    public static IApplicationBuilder UseAiGateway(this IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseMiddleware<PathPatternMiddleware>();
        applicationBuilder.UseMiddleware<AiGatewayMiddleware>();
        return applicationBuilder;
    }

    public static IServiceCollection AddAiGatewayFundamentalComponents(this IServiceCollection services)
    {
        services.AddSingleton<IPathValidator, DefaultPathValidator>();
        services.AddSingleton<IPathBuilder, DefaultPathBuilder>();
        services.AddSingleton<IResultBuilder, DefaultResultBuilder>();
        services.AddSingleton<IPathParser, DefaultPathParser>();
        services.AddSingleton<IProviderRouter, DefaultProviderRouter>();
        services.AddHttpForwarder();
        RegisterProviderStrategies(services);
        return services;
    }

    private static void RegisterProviderStrategies(IServiceCollection services)
    {
        var strategyType = typeof(IProviderStrategy);
        var strategies = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && strategyType.IsAssignableFrom(type))
            .ToList();

        foreach (var strategy in strategies)
        {
            services.AddSingleton(strategyType, strategy);
        }
    }
}