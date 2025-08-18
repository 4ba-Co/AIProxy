using Microsoft.AspNetCore.Connections;

namespace KestrelAIProxy.Common;

public interface IKestrelMiddleware
{
    Task InvokeAsync(ConnectionDelegate next, ConnectionContext context);
}