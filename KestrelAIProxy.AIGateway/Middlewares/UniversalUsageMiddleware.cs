using KestrelAIProxy.AIGateway.Core;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
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
        var isStreaming = IsStreamingRequest(context);
        
        logger.LogDebug("Intercepting {Provider} request {RequestId}, Streaming: {IsStreaming}", 
            tracker.ProviderName, requestId, isStreaming);

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
            isStreaming,
            tracker.ProviderName,
            tracker.OnUsageDetectedAsync,
            logger,
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

    private static bool IsStreamingRequest(HttpContext context)
    {
        // Check various indicators for streaming requests
        
        // 1. Accept header
        var acceptHeader = context.Request.Headers.Accept.ToString();
        if (acceptHeader.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Query parameters
        if (context.Request.Query.TryGetValue("stream", out var streamValue))
        {
            return string.Equals(streamValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        // 3. Content-Type might indicate SSE expectations
        var contentType = context.Request.ContentType ?? "";
        if (contentType.Contains("stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 4. Custom headers that indicate streaming
        if (context.Request.Headers.TryGetValue("X-Stream", out var xStreamValue))
        {
            return string.Equals(xStreamValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        // Default to non-streaming
        return false;
    }

    private static string GenerateRequestId()
    {
        return $"req_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
    }
}