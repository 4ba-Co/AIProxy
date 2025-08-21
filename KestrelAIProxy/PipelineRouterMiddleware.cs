namespace KestrelAIProxy;

public sealed class PipelineRouterMiddleware(RequestDelegate next, ILogger<PipelineRouterMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();

        switch (GetPipelineType(path))
        {
            case PipelineType.Api:
                logger.LogDebug("Route to api pipe: {Path}", path);
                context.Items["PipelineType"] = PipelineType.Api;
                break;

            case PipelineType.Static:
                logger.LogDebug("Route to static pipe: {Path}", path);
                context.Items["PipelineType"] = PipelineType.Static;
                break;

            case PipelineType.Gateway:
                logger.LogDebug("Route to gateway pipe: {Path}", path);
                context.Items["PipelineType"] = PipelineType.Gateway;
                break;

            default:
                logger.LogWarning("Unknown, route to default: {Path}", path);
                break;
        }

        await next(context);
    }

    private static PipelineType GetPipelineType(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return PipelineType.Static;


        if (path.StartsWith("/providers") || path.StartsWith("/health"))
            return PipelineType.Api;

        if (path == "/" ||
            path.StartsWith("/favicon.ico") ||
            path.Contains('.'))
            return PipelineType.Static;

        // AI网关管道
        return PipelineType.Gateway;
    }
}

public enum PipelineType
{
    Api,
    Static,
    Gateway
}