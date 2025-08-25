using KestrelAIProxy.AIGateway.Core.Models;
using Microsoft.AspNetCore.Http;

namespace KestrelAIProxy.AIGateway.Core.Interfaces;

public interface IProviderStrategy
{
    string ProviderName { get; }
    Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath);
}