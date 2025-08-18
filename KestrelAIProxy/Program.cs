using System.Collections.Immutable;
using KestrelAIProxy.AIGateway;
using KestrelAIProxy.Common;
using KestrelAIProxy.AIGateway.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseSerilog();
builder.Services.AddAiGatewayFundamentalComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/providers", (IProviderRouter providerRouter) => providerRouter.GetAllProviderNames().ToImmutableSortedSet());

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHealthChecks("/health");
app.UseSerilogRequestLogging();
app.UseAiGateway();

app.Run();