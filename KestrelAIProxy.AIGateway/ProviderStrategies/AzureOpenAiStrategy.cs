using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.ProviderStrategies;

public sealed class AzureOpenAiStrategy(
    IResultBuilder resultBuilder,
    IPathValidator pathValidator,
    ILogger<AzureOpenAiStrategy> logger) : IProviderStrategy
{
    public string ProviderName => "azure-openai";

    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        var segments = parsedPath.ProviderSegments;

        if (!pathValidator.ValidateMinimumSegments(segments, 2, out var errorMessage))
        {
            logger.LogWarning("{Provider} validation failed: {Error}", ProviderName, errorMessage);
            return Task.FromResult(resultBuilder.CreateErrorResult(
                $"{ProviderName} requires at least runtime and region. Format: /azure-openai/{{resource_name}}/{{deployment_name}}/... Error: {errorMessage}"));
        }

        var resourceName = segments[0];
        var deploymentName = segments[1];

        if (!pathValidator.ValidateNotEmpty(resourceName, "resource_name", out var resourceNameError))
        {
            return Task.FromResult(resultBuilder.CreateErrorResult($"{ProviderName} {resourceNameError}"));
        }

        if (!pathValidator.ValidateNotEmpty(deploymentName, "deployment_name", out var deploymentNameError))
        {
            return Task.FromResult(resultBuilder.CreateErrorResult($"{ProviderName} {deploymentNameError}"));
        }

        var remainingSegments = segments.Length > 2 ? segments[2..] : [];
        var targetHost = $"{resourceName}.openai.azure.com";

        logger.LogDebug(
            "{Provider} mapping: resourceName={ResourceName}, deploymentName={DeploymentName}, target={Target}",
            ProviderName, resourceName, deploymentName, targetHost);

        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: targetHost,
            pathSegments: ["openai", "deployments", deploymentName, ..remainingSegments],
            queryString: parsedPath.QueryString,
            additionalMetadata: new Dictionary<string, object>
            {
                ["ResourceName"] = resourceName,
                ["DeploymentName"] = deploymentName,
                ["OriginalPath"] = parsedPath.OriginalPath
            }));
    }
}