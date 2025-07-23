using System.Net;
using System.Text.Json;

namespace AIProxy.Proxy.Middleware;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred for request {RequestId}",
                context.Items["RequestId"]);
            
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var (statusCode, message) = exception switch
        {
            TaskCanceledException => (HttpStatusCode.RequestTimeout, "Request timeout"),
            HttpRequestException => (HttpStatusCode.BadGateway, "Upstream service error"),
            ArgumentException => (HttpStatusCode.BadRequest, "Invalid request"),
            _ => (HttpStatusCode.InternalServerError, "Internal server error")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = message,
            requestId = context.Items["RequestId"]?.ToString(),
            timestamp = DateTime.UtcNow
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}