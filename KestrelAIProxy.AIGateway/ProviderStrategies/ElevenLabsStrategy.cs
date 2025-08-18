using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.ProviderStrategies;

public sealed class ElevenLabsStrategy(
    IResultBuilder resultBuilder,
    ILogger<ElevenLabsStrategy> logger)
    : IProviderStrategy
{
    public string ProviderName => "elevenlabs";

    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        var segments = parsedPath.ProviderSegments;

        logger.LogDebug("{Provider} mapping: segments=[{Segments}]", ProviderName, string.Join(", ", segments));

        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: "api.elevenlabs.io",
            pathSegments: segments.Length > 0 ? segments : [],
            queryString: parsedPath.QueryString,
            additionalHeaders: [],
            additionalMetadata: []));
    }
}