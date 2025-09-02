using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace KestrelAIProxy.Common;

public static class AspSentryExtensions
{
    public static void AddSentry(this WebApplicationBuilder hostBuilder)
    {
        hostBuilder.WebHost.UseSentry();
    }
}