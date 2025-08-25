using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.ProviderStrategies;

public sealed class AwsBedrockStrategy(
    IResultBuilder resultBuilder,
    IPathValidator pathValidator,
    ILogger<AwsBedrockStrategy> logger)
    : IProviderStrategy
{
    public string ProviderName => "aws-bedrock";

    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        var segments = parsedPath.ProviderSegments;

        if (!pathValidator.ValidateMinimumSegments(segments, 2, out var errorMessage))
        {
            logger.LogWarning("AWS Bedrock validation failed: {Error}", errorMessage);
            return Task.FromResult(resultBuilder.CreateErrorResult(
                $"AWS Bedrock requires at least runtime and region. Format: /aws-bedrock/{{runtime}}/{{region}}/... Error: {errorMessage}"));
        }

        var runtime = segments[0];
        var region = segments[1];

        if (!pathValidator.ValidateNotEmpty(runtime, "runtime", out var runtimeError))
        {
            return Task.FromResult(resultBuilder.CreateErrorResult($"AWS Bedrock {runtimeError}"));
        }

        if (!pathValidator.ValidateNotEmpty(region, "region", out var regionError))
        {
            return Task.FromResult(resultBuilder.CreateErrorResult($"AWS Bedrock {regionError}"));
        }

        var remainingSegments = segments.Length > 2 ? segments[2..] : [];
        var targetHost = $"{runtime}.{region}.amazonaws.com";

        logger.LogDebug("{Provider} mapping: runtime={Runtime}, region={Region}, target={Target}",
            ProviderName, runtime, region, targetHost);

        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: targetHost,
            pathSegments: remainingSegments,
            queryString: parsedPath.QueryString,
            additionalMetadata: new Dictionary<string, object>
            {
                ["Runtime"] = runtime,
                ["Region"] = region,
                ["OriginalPath"] = parsedPath.OriginalPath
            }));
    }
}