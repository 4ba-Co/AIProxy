using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.ProviderStrategies;

public sealed class DeepseekStrategy(
    IResultBuilder resultBuilder,
    ILogger<DeepseekStrategy> logger)
    : IProviderStrategy
{
    public string ProviderName => "deepseek";

    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        var segments = parsedPath.ProviderSegments;

        logger.LogDebug("{Provider} mapping: segments=[{Segments}]", ProviderName, string.Join(", ", segments));

        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: "api.deepseek.com",
            pathSegments: segments.Length > 0 ? segments : [],
            queryString: parsedPath.QueryString,
            additionalHeaders: [],
            additionalMetadata: []));
    }
}