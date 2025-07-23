using Microsoft.AspNetCore.SignalR.Client;

namespace AIProxy.Proxy;

public sealed class AlwaysRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        return TimeSpan.FromSeconds(2);
    }
}