using StackExchange.Redis.Extensions.Core.Abstractions;

namespace AIProxy.Proxy.Services;

public interface ITokenValidationService
{
    Task<bool> ValidateTokenAsync(string token, string provider);
}

public sealed class TokenValidationService(IRedisClient redis)
    : ITokenValidationService
{
    private const string TokenPrefix = "UserToken:";

    public async Task<bool> ValidateTokenAsync(string token, string provider)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        var db = redis.GetDefaultDatabase();
        var cacheKey = $"{TokenPrefix}{token}";

        // 从 Redis 中获取 token 信息
        var cachedToken = await db.GetAsync<string>(cacheKey);
        if (!string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken == provider;
        }

        return false;
    }
}