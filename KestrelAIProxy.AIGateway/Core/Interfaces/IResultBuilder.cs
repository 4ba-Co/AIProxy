using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core.Interfaces;

public interface IResultBuilder
{
    ParseResult CreateErrorResult(string errorMessage);

    ParseResult CreateSuccessResult(
        string providerName,
        string targetHost,
        string[] pathSegments,
        string? queryString = null,
        string scheme = "https",
        Dictionary<string, string>? additionalHeaders = null,
        Dictionary<string, object>? additionalMetadata = null);
}