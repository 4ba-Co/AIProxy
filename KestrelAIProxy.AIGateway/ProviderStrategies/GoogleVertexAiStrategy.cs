using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.ProviderStrategies;

public class GoogleVertexAiStrategy(
    IResultBuilder resultBuilder,
    IPathValidator pathValidator,
    ILogger<GoogleVertexAiStrategy> logger) : IProviderStrategy
{
    public string ProviderName => "google-vertex-ai";

    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        var segments = parsedPath.ProviderSegments;

        if (!pathValidator.ValidateMinimumSegments(segments, 4, out var errorMessage))
        {
            logger.LogWarning("{Provider} validation failed: {Error}", ProviderName, errorMessage);
            return Task.FromResult(resultBuilder.CreateErrorResult(
                $"{ProviderName} requires at least runtime and region. Format: /google-vertex-ai/projects/{{project_id}}/locations/{{location}}/... Error: {errorMessage}"));
        }

        var projectId = segments[1];
        var location = segments[3];

        if (!pathValidator.ValidateNotEmpty(projectId, "project_id", out var projectIdError))
        {
            return Task.FromResult(resultBuilder.CreateErrorResult($"{ProviderName} {projectIdError}"));
        }

        if (!pathValidator.ValidateNotEmpty(location, "location", out var locationError))
        {
            return Task.FromResult(resultBuilder.CreateErrorResult($"{ProviderName} {locationError}"));
        }

        var remainingSegments = segments.Length > 4 ? segments[4..] : [];
        var targetHost = $"{location}-aiplatform.googleapis.com";

        logger.LogDebug("{Provider} mapping: projectId={ProjectId}, location={Location}, target={Target}",
            ProviderName, projectId, location, targetHost);

        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: targetHost,
            pathSegments: ["v1", "projects", projectId, "locations", location, .. remainingSegments],
            queryString: parsedPath.QueryString,
            additionalMetadata: new Dictionary<string, object>
            {
                ["ProjectId"] = projectId,
                ["Location"] = location,
                ["OriginalPath"] = parsedPath.OriginalPath
            }));
    }
}