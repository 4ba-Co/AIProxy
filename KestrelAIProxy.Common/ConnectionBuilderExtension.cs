using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace KestrelAIProxy.Common;

public static class ConnectionBuilderExtensions
{
    public static IConnectionBuilder Use<TMiddleware>(this IConnectionBuilder builder)
        where TMiddleware : IKestrelMiddleware
    {
        var middleware = ActivatorUtilities.GetServiceOrCreateInstance<TMiddleware>(builder.ApplicationServices);
        return builder.Use(middleware);
    }

    public static IConnectionBuilder Use(this IConnectionBuilder builder, IKestrelMiddleware middleware)
    {
        return builder.Use(next => context => middleware.InvokeAsync(next, context));
    }
}