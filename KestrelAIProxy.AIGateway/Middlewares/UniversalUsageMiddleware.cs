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

        await next(context);

        if (context.Items.TryGetValue(CustomTransformer.ResponseCopyStreamManagerKey, out var managerObject)
            && managerObject is ResponseCopyStreamManager streamManager)
        {
            try
            {
                // RAII: Use the managed stream with automatic lifecycle management
                if (streamManager.IsAccessible)
                {
                    streamManager.ResetPosition();
                    await using var universalStreamInvoker = new UniversalResponseInvoker(
                        requestId,
                        tracker.ProviderName,
                        context,
                        streamManager.CopyStream,
                        processor,
                        tracker.OnUsageDetectedAsync);
                    await universalStreamInvoker.InvokeAsync(context.RequestAborted);
                }
                else
                {
                    logger.LogWarning("Copy stream manager reports stream is not accessible - RequestId: {RequestId}", requestId);
                }
            }
            catch (ObjectDisposedException)
            {
                logger.LogWarning("Copy stream was disposed before usage processing - RequestId: {RequestId}", requestId);
            }
            // Note: No manual cleanup needed - RAII manager handles disposal via RequestAborted registration
        }
    }

    private IResponseProcessor? GetResponseProcessor(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => serviceProvider.GetService<OpenAiResponseProcessor>(),
            "openrouter" => serviceProvider.GetService<OpenAiResponseProcessor>(),
            "anthropic" => serviceProvider.GetService<AnthropicResponseProcessor>(),
            _ => null
        };
    }

    private static string GenerateRequestId()
    {
        return $"req_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
    }
}