using Microsoft.AspNetCore.SignalR.Client;

namespace AIProxy.Proxy;

public sealed class HubConnectionHandler(IConfiguration configuration) : IHostApplicationLifetime
{
    

    public void StopApplication()
    {
        throw new NotImplementedException();
    }

    public CancellationToken ApplicationStarted { get; }
    public CancellationToken ApplicationStopped { get; }
    public CancellationToken ApplicationStopping { get; }
}