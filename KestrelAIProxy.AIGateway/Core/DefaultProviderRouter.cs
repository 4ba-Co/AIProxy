using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class DefaultProviderRouter : IProviderRouter
{
    private readonly Dictionary<string, IProviderStrategy> _strategies;
    private readonly ILogger<DefaultProviderRouter> _logger;

    public DefaultProviderRouter(
        IEnumerable<IProviderStrategy> strategies,
        ILogger<DefaultProviderRouter> logger)
    {
        _logger = logger;

        _strategies = strategies.ToDictionary(
            s => s.ProviderName.ToLowerInvariant(),
            s => s,
            StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("Registered {Count} provider strategies: {Providers}",
            _strategies.Count,
            string.Join(", ", _strategies.Keys));
    }

    public async Task<ParseResult> RouteAsync(HttpContext context, ParsedPath parsedPath)
    {
        if (string.IsNullOrEmpty(parsedPath.ProviderName))
        {
            return new ParseResult
            {
                IsValid = false,
                ErrorMessage = "Provider name is required in the path"
            };
        }

        var providerKey = parsedPath.ProviderName.ToLowerInvariant();

        if (!_strategies.TryGetValue(providerKey, out var strategy))
        {
            return new ParseResult
            {
                IsValid = false,
                ErrorMessage =
                    $"Unknown provider: {parsedPath.ProviderName}. Available providers: {string.Join(", ", _strategies.Keys)}"
            };
        }

        _logger.LogDebug("Routing to provider {Provider} for path {Path}",
            strategy.ProviderName, parsedPath.OriginalPath);

        try
        {
            var result = await strategy.ParseAsync(context, parsedPath);

            if (result.IsValid)
            {
                _logger.LogInformation("Successfully routed {Provider}: {OriginalPath} -> {TargetUri}",
                    strategy.ProviderName, parsedPath.OriginalPath, result.TargetUri);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in provider strategy {Provider}", strategy.ProviderName);
            return new ParseResult
            {
                IsValid = false,
                ErrorMessage = $"Error processing request with provider {strategy.ProviderName}: {ex.Message}"
            };
        }
    }

    public bool HasProvider(string providerName)
    {
        return _strategies.ContainsKey(providerName.ToLowerInvariant());
    }

    public IEnumerable<string> GetAllProviderNames()
    {
        return _strategies.Values.Select(s => s.ProviderName);
    }
}