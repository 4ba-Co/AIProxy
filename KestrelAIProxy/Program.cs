using System.Collections.Immutable;
using KestrelAIProxy.AIGateway;
using KestrelAIProxy.Common;
using KestrelAIProxy.AIGateway.Extensions;
using Serilog;
using KestrelAIProxy;
using KestrelAIProxy.AIGateway.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseSerilog();
builder.Services.AddAiGatewayFundamentalComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.Map("/health", ab => { ab.UseHealthChecks(null); });
app.Map("/providers", apiApp =>
{
    apiApp.UseRouting();
    apiApp.UseEndpoints(endpoints =>
    {
        endpoints.MapGet("/", (IProviderRouter providerRouter) =>
            providerRouter.GetAllProviderNames().ToImmutableSortedSet());
    });
});

app.UsePipelineRouter();

app.UseStaticPipeline(staticApp =>
{
    staticApp.UseDefaultFiles();
    staticApp.UseStaticFiles();
});


app.UseGatewayPipeline(gatewayApp => { gatewayApp.UseAiGateway(); });

app.Run();