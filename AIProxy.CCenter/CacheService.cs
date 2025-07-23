using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace AIProxy.CCenter;

public sealed class CacheService(HybridCache cache, AiProxyDbContext db)
{
    public Task<bool> GetUserRequestTokenAsync(string token)
    {
        var cacheKey = $"UserToken:{token}";
        return cache.GetOrCreateAsync(cacheKey, async cancellationToken =>
        {
            var userRequestToken = await db.UserRequestTokens.Where(x => x.Token == token)
                .FirstOrDefaultAsync(cancellationToken);
            return userRequestToken != null && userRequestToken.ExpiredAt > DateTime.UtcNow;
        }).AsTask();
    }

    public Task<List<string>> GetProvidersAsync()
    {
        const string cacheKey = "Providers";
        return cache.GetOrCreateAsync(cacheKey, async cancellationToken =>
        {
            var providers = await db.Providers.Select(x => x.Value).ToListAsync(cancellationToken: cancellationToken);
            return providers;
        }).AsTask();
    }
}