using AIProxy.Common;
using AIProxy.Proxy;
using AIProxy.Proxy.Middleware;
using Microsoft.Extensions.Caching.Hybrid;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddHybridCache(options =>
// {
//     options.DefaultEntryOptions = new HybridCacheEntryOptions
//     {
//         LocalCacheExpiration = TimeSpan.FromMinutes(1),
//         
//     };
//     
// });

var clusters = new[]
{
    new ClusterConfig
    {
        ClusterId = "ai-proxy-sg",
        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "sg", new DestinationConfig() { Address = "https://ai-proxy.sg.4ba.ai" } }
        }
    },
    new ClusterConfig
    {
        ClusterId = "ai-proxy-na-cf",
        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "cf-na",
                new DestinationConfig()
                    { Address = "https://gateway.ai.cloudflare.com/v1/f201992af48c42f242036188814036ce/na-us-01" }
            }
        }
    },
    new ClusterConfig
    {
        ClusterId = "ai-proxy-sg-cf",
        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "cf-sg",
                new DestinationConfig()
                    { Address = "https://gateway.ai.cloudflare.com/v1/f201992af48c42f242036188814036ce/asia-sg-01" }
            }
        }
    }
};

if (!Enum.TryParse<ReverseMode>(builder.Configuration["ReverseMode"], out var reverseMode))
{
    throw new ArgumentException("Invalid ReverseMode specified in configuration.");
}

var routes = new ProxyRoutesFactory(reverseMode).Produce();

builder.Services.AddReverseProxy()
    .LoadFromMemory(routes, clusters);

builder.Services.AddHealthChecks();

// builder.Services.AddAuthorization(options =>
// {
//     options.AddPolicy("custom-token", policy => { policy.Requirements.Add(new CustomToken()); });
// });

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<MetricsMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();