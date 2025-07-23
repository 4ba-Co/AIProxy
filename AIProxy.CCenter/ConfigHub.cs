using Microsoft.AspNetCore.SignalR;

namespace AIProxy.CCenter;

public sealed class ConfigHub(CacheService cacheService) : Hub
{
    public async Task SearchToken(string token)
    {
        var result = await cacheService.GetUserRequestTokenAsync(token);
        await Clients.Caller.SendAsync("ReceiveToken", result);
    }

    public async Task GetProviders()
    {
        var providers = await cacheService.GetProvidersAsync();
        await Clients.Caller.SendAsync("ReceiveProviders", providers);
    }
}