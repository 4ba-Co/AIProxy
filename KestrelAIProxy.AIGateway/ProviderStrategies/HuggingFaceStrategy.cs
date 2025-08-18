using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.ProviderStrategies;

public sealed class HuggingFaceStrategy(
    IResultBuilder resultBuilder,
    ILogger<HuggingFaceStrategy> logger)
    : IProviderStrategy
{
    public string ProviderName => "huggingface";

    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        var segments = parsedPath.ProviderSegments;

        logger.LogDebug("{Provider} mapping: segments=[{Segments}]", ProviderName, string.Join(", ", segments));

        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: "api-inference.huggingface.co",
            pathSegments: segments.Length > 0 ? segments : [],
            queryString: parsedPath.QueryString,
            additionalHeaders: [],
            additionalMetadata: []));
    }
}