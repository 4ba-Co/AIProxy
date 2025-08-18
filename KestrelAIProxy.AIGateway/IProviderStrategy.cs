using Microsoft.AspNetCore.Http;

namespace KestrelAIProxy.AIGateway;

public interface IProviderStrategy
{
    string ProviderName { get; }
    Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath);
}