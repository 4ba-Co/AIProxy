using System.Collections.Immutable;
using KestrelAIProxy.AIGateway;
using KestrelAIProxy.Common;
using KestrelAIProxy.AIGateway.Extensions;
using Serilog;
using KestrelAIProxy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseSerilog();
builder.Services.AddAiGatewayFundamentalComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.Map("/health", ab => { ab.UseHealthChecks(null); });

app.UsePipelineRouter();

app.UseStaticPipeline(staticApp =>
{
    staticApp.UseDefaultFiles();
    staticApp.UseStaticFiles();
});

app.UseApiPipeline(apiApp =>
{
    apiApp.UseRouting();
    apiApp.UseEndpoints(endpoints =>
    {
        endpoints.MapGet("/", (IProviderRouter providerRouter) =>
            providerRouter.GetAllProviderNames().ToImmutableSortedSet());
    });
});

app.UseGatewayPipeline(gatewayApp => { gatewayApp.UseAiGateway(); });

app.Run();