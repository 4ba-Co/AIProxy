using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.AspNetCore.Http;

namespace KestrelAIProxy.AIGateway.Core.Interfaces;

public interface IProviderRouter
{
    Task<ParseResult> RouteAsync(HttpContext context, ParsedPath parsedPath);
    bool HasProvider(string providerName);
    IEnumerable<string> GetAllProviderNames();
}