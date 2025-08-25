using KestrelAIProxy.AIGateway.Core;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Middlewares;
using KestrelAIProxy.AIGateway.ProviderStrategies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace KestrelAIProxy.AIGateway.Extensions;

public static class AiGatewayExtensions
{
    public static IApplicationBuilder UseAiGateway(this IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseMiddleware<PathPatternMiddleware>();
        applicationBuilder.UseMiddleware<SseInterceptorMiddleware>();
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

    [UnconditionalSuppressMessage("Trimming", "IL2072:Validate parameters correctly", Justification = "Provider strategies are explicitly registered")]
    private static void RegisterProviderStrategies(IServiceCollection services)
    {
        // 编译时注册所有策略类型，避免运行时反射
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

    private static void RegisterStrategy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] TStrategy>(IServiceCollection services) where TStrategy : class, IProviderStrategy
    {
        services.AddSingleton<IProviderStrategy, TStrategy>();
    }
}