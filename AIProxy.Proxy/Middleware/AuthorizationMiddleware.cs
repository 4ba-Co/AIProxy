using AIProxy.Proxy.Services;

namespace AIProxy.Proxy.Middleware;

public sealed class AuthorizationMiddleware(RequestDelegate next, ITokenValidationService tokenService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (path == null || !IsProviderEndpoint(path, out var provider))
        {
            await next(context);
            return;
        }

        // 检查 Authorization header
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Authorization header is required");
            return;
        }

        // 解析 Bearer token
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid authorization format. Expected 'Bearer <token>'");
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Token is required");
            return;
        }

        // 验证 token 是否存在于 Redis 中
        var isValidToken = await tokenService.ValidateTokenAsync(token, provider);
        if (!isValidToken)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid token or provider not matched");
            return;
        }

        await next(context);
    }



    private static bool IsProviderEndpoint(string path, out string provider)
    {
        // 检查路径是否匹配 /{provider}/{**else} 模式
        // 这里我们检查路径是否包含至少两个段，第一个段是 provider
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        provider = segments[0];
        return segments.Length >= 2;
    }
}

public sealed class AuthorizationConfig
{
    public bool EnableAuthorization { get; set; } = true;
}
