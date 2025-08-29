using KestrelAIProxy.AIGateway.Core;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Middlewares;

public sealed class UniversalUsageMiddleware(
    RequestDelegate next,
    IEnumerable<IUsageTracker> usageTrackers,
    IServiceProvider serviceProvider,
    ILogger<UniversalUsageMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        logger.LogDebug("UniversalUsageMiddleware invoked for path: {Path}", context.Request.Path.Value);

        var parsedPath = context.GetParsedPath();
        if (parsedPath == null)
        {
            logger.LogDebug("No parsed path found, skipping usage tracking");
            await next(context);
            return;
        }

        logger.LogDebug("Parsed path found: Provider={Provider}, Segments=[{Segments}]",
            parsedPath.ProviderName, string.Join(",", parsedPath.ProviderSegments));

        // Find the appropriate usage tracker for this request
        var tracker = usageTrackers.FirstOrDefault(t => t.ShouldTrack(context, parsedPath));
        if (tracker == null)
        {
            await next(context);
            return;
        }

        var requestId = GenerateRequestId();

        logger.LogDebug("Intercepting {Provider} request {RequestId}",
            tracker.ProviderName, requestId);

        // Get the appropriate response processor
        var processor = GetResponseProcessor(tracker.ProviderName);
        if (processor == null)
        {
            logger.LogWarning("No response processor found for provider {Provider}", tracker.ProviderName);
            await next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;

        await using var universalStream = new UniversalResponseStream(
            originalBodyStream,
            processor,
            requestId,
            tracker.ProviderName,
            tracker.OnUsageDetectedAsync,
            logger,
            context,
            context.RequestAborted);

        context.Response.Body = universalStream;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private IResponseProcessor? GetResponseProcessor(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => serviceProvider.GetService<OpenAiResponseProcessor>(),
            "anthropic" => serviceProvider.GetService<AnthropicResponseProcessor>(),
            _ => null
        };
    }

    private static string GenerateRequestId()
    {
        return $"req_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
    }
}