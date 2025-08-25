using KestrelAIProxy.AIGateway.Core;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Middlewares;

public sealed class SseInterceptorMiddleware(RequestDelegate next, ILogger<SseInterceptorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var originalBodyStream = context.Response.Body;

        await using var customStream = new SseParsingStream(originalBodyStream, ParseLine);
        context.Response.Body = customStream;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }


    private Task ParseLine(string line)
    {
        if (!line.StartsWith("data:")) return Task.CompletedTask;
        var chunk = line[5..].Trim();
        if (!string.IsNullOrEmpty(chunk))
        {
            logger.LogInformation("Intercepted SSE data: {Data}", chunk);

            // do something else
        }

        return Task.CompletedTask;
    }
}