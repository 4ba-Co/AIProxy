using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.ProviderStrategies;

public sealed class PerplexityAiStrategy(
    IResultBuilder resultBuilder,
    ILogger<PerplexityAiStrategy> logger) : IProviderStrategy
{
    public string ProviderName => "perplexity-ai";

    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        var segments = parsedPath.ProviderSegments;

        logger.LogDebug("{Provider} mapping: segments=[{Segments}]", ProviderName, string.Join(", ", segments));

        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: "api.perplexity.ai",
            pathSegments: segments.Length > 0 ? segments : [],
            queryString: parsedPath.QueryString,
            additionalHeaders: [],
            additionalMetadata: []));
    }
}