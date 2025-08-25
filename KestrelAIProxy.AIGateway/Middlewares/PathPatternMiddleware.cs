using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Middlewares;

public class PathPatternMiddleware(
    RequestDelegate next,
    IPathParser pathParser,
    IProviderRouter providerRouter,
    ILogger<PathPatternMiddleware> logger)
{
    private readonly ILogger _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            var originalPath = context.Request.Path.Value ?? "/";
            var queryString = context.Request.QueryString.Value ?? "";
            var parsedPath = pathParser.ParsePath(originalPath, queryString);
            context.SetParsedPath(parsedPath);
            _logger.LogDebug(
                "Parsed path: {OriginalPath} -> Provider: {Provider}, Segments: [{Segments}], Query: {Query}",
                parsedPath.OriginalPath,
                parsedPath.ProviderName,
                string.Join(", ", parsedPath.Segments),
                parsedPath.QueryString);
            var parseResult = await providerRouter.RouteAsync(context, parsedPath);
            context.SetParseResult(parseResult);
            if (!parseResult.IsValid)
            {
                _logger.LogWarning("Failed to parse request: {ErrorMessage}", parseResult.ErrorMessage);
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync(parseResult.ErrorMessage);
                return;
            }

            _logger.LogInformation("Successfully parsed request: {Provider} -> {TargetUri}",
                parseResult.Metadata?.GetValueOrDefault("Provider", "Unknown"),
                parseResult.TargetUri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during request parsing");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error during request parsing");
            return;
        }

        await next(context);
    }
}