using AIProxy.CCenter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<CacheService>();
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddDbContextPool<AiProxyDbContext>(contextBuilder =>
{
    contextBuilder.UseNpgsql(builder.Configuration.GetConnectionString("Supabase"), optionsBuilder =>
    {
        optionsBuilder.EnableRetryOnFailure();
        optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        optionsBuilder.CommandTimeout(180);
    });
});

builder.Services.AddSignalR().AddMessagePackProtocol();

var app = builder.Build();

app.MapHub<ConfigHub>("/configHub");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Run();