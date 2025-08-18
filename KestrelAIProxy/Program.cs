using KestrelAIProxy.Common;
using KestrelAIProxy.AIGateway.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseSerilog();
builder.Services.AddAiGatewayFundamentalComponents();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseAiGateway();

app.Run();