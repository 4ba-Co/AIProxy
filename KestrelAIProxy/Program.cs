using KestrelAIProxy;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Extensions;
using KestrelAIProxy.Common;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseSerilog();
builder.Services.AddAiGatewayFundamentalComponents();
builder.Services.AddHealthChecks();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = JsonContext.Default;
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.Map("/health", ab => { ab.UseHealthChecks(null); });
app.Map("/providers", apiApp =>
{
    apiApp.UseRouting();
    apiApp.UseEndpoints(endpoints =>
    {
        endpoints.MapGet("/", (IProviderRouter providerRouter) =>
            providerRouter.GetAllProviderNames().OrderBy(x => x).ToArray());
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