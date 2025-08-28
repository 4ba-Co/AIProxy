using System.Diagnostics.CodeAnalysis;

using KestrelAIProxy.AIGateway.Core;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Middlewares;
using KestrelAIProxy.AIGateway.ProviderStrategies;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace KestrelAIProxy.AIGateway.Extensions;

public static class AiGatewayExtensions
{
    public static IApplicationBuilder UseAiGateway(this IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseMiddleware<PathPatternMiddleware>();
        applicationBuilder.UseMiddleware<UniversalUsageMiddleware>();
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
        
        // Pricing services
        services.AddSingleton<IAnthropicPricingService, AnthropicPricingService>();
        
        // Usage tracking services
        services.AddSingleton<IUsageTracker, OpenAiUsageTracker>();
        services.AddSingleton<IUsageTracker, AnthropicUsageTracker>();
        
        // Response processors
        services.AddSingleton<OpenAiResponseProcessor>();
        services.AddSingleton<AnthropicResponseProcessor>();
        
        services.AddHttpForwarder();
        RegisterProviderStrategies(services);
        return services;
    }

    private static void RegisterProviderStrategies(IServiceCollection services)
    {
        RegisterStrategy<AnthropicStrategy>(services);
        RegisterStrategy<AwsBedrockStrategy>(services);
        RegisterStrategy<AzureOpenAiStrategy>(services);
        RegisterStrategy<CartesiaStrategy>(services);
        RegisterStrategy<CerebrasStrategy>(services);
        RegisterStrategy<CohereStrategy>(services);
        RegisterStrategy<DeepbricksStrategy>(services);
        RegisterStrategy<DeepSeekStrategy>(services);
        RegisterStrategy<ElevenLabsStrategy>(services);
        RegisterStrategy<FireworksStrategy>(services);
        RegisterStrategy<GoogleAiStudioStrategy>(services);
        RegisterStrategy<GoogleVertexAiStrategy>(services);
        RegisterStrategy<GrokStrategy>(services);
        RegisterStrategy<GroqStrategy>(services);
        RegisterStrategy<HuggingFaceStrategy>(services);
        RegisterStrategy<HyperbolicStrategy>(services);
        RegisterStrategy<JinaDeepSearchStrategy>(services);
        RegisterStrategy<JinaStrategy>(services);
        RegisterStrategy<MistralStrategy>(services);
        RegisterStrategy<OpenAiStrategy>(services);
        RegisterStrategy<OpenRouterStrategy>(services);
        RegisterStrategy<PerplexityAiStrategy>(services);
        RegisterStrategy<PoeStrategy>(services);
        RegisterStrategy<ReplicateStrategy>(services);
        RegisterStrategy<TogetherStrategy>(services);
        RegisterStrategy<VercelStrategy>(services);
    }

    private static void RegisterStrategy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                    DynamicallyAccessedMemberTypes.PublicMethods)]
    TStrategy>(IServiceCollection services) where TStrategy : class, IProviderStrategy
    {
        services.AddSingleton<IProviderStrategy, TStrategy>();
    }
}