using System.Diagnostics;

namespace AIProxy.Proxy.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("D");
        
        context.Items["RequestId"] = requestId;
        
        logger.LogInformation("Request started: {RequestId} {Method} {Path} {QueryString} {RemoteIp}",
            requestId,
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Connection.RemoteIpAddress);

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            logger.LogInformation("Request completed: {RequestId} {StatusCode} {Duration}ms",
                requestId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}