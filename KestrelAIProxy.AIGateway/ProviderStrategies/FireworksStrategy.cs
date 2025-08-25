using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.ProviderStrategies;

public sealed class FireworksStrategy(
    IResultBuilder resultBuilder,
    ILogger<FireworksStrategy> logger)
    : IProviderStrategy
{
    public string ProviderName => "fireworks";

    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        var segments = parsedPath.ProviderSegments;

        logger.LogDebug("{Provider} mapping: segments=[{Segments}]", ProviderName, string.Join(", ", segments));

        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: "api.fireworks.ai",
            pathSegments: ["inference", .. segments.Length > 0 ? segments : []],
            queryString: parsedPath.QueryString,
            additionalHeaders: [],
            additionalMetadata: []));
    }
}