using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class DefaultResultBuilder(IPathBuilder pathBuilder) : IResultBuilder
{
    public ParseResult CreateErrorResult(string errorMessage)
    {
        return new ParseResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }

    public ParseResult CreateSuccessResult(
        string providerName,
        string targetHost,
        string[] pathSegments,
        string? queryString = null,
        string scheme = "https",
        Dictionary<string, string>? additionalHeaders = null,
        Dictionary<string, object>? additionalMetadata = null
    )
    {
        var metadata = new Dictionary<string, object>
        {
            ["Provider"] = providerName
        };

        foreach (var kvp in additionalMetadata ?? [])
        {
            metadata[kvp.Key] = kvp.Value;
        }

        return new ParseResult
        {
            IsValid = true,
            TargetHost = targetHost,
            TargetPath = pathBuilder.BuildPath(pathSegments, queryString),
            AdditionalHeaders = additionalHeaders,
            Metadata = metadata
        };
    }
}