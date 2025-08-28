using System.Runtime.CompilerServices;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KestrelAIProxy.AIGateway.Core.Pricing;

/// <summary>
/// High-performance Anthropic pricing calculator with caching
/// </summary>
public sealed class AnthropicPricingCalculator : IPricingCalculator
{
    private readonly ILogger<AnthropicPricingCalculator> _logger;
    private readonly PricingCache _cache = new();
    
    // Pre-calculated pricing for common models (avoids dictionary lookups)
    private static readonly Dictionary<string, PricingConfig> DefaultPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-3-5-sonnet-20241022"] = new PricingConfig { InputPricePerMillion = 3.00m, OutputPricePerMillion = 15.00m, CachePricePerMillion = 3.75m },
        ["claude-3-5-sonnet-20240620"] = new PricingConfig { InputPricePerMillion = 3.00m, OutputPricePerMillion = 15.00m, CachePricePerMillion = 3.75m },
        ["claude-3-5-haiku-20241022"] = new PricingConfig { InputPricePerMillion = 1.00m, OutputPricePerMillion = 5.00m, CachePricePerMillion = 1.25m },
        ["claude-3-opus-20240229"] = new PricingConfig { InputPricePerMillion = 15.00m, OutputPricePerMillion = 75.00m, CachePricePerMillion = 18.75m },
        ["claude-3-sonnet-20240229"] = new PricingConfig { InputPricePerMillion = 3.00m, OutputPricePerMillion = 15.00m, CachePricePerMillion = 3.75m },
        ["claude-3-haiku-20240307"] = new PricingConfig { InputPricePerMillion = 0.25m, OutputPricePerMillion = 1.25m, CachePricePerMillion = 0.3125m }
    };

    public string ProviderName => "anthropic";

    public AnthropicPricingCalculator(ILogger<AnthropicPricingCalculator> logger)
    {
        _logger = logger;
        
        // Initialize cache with default pricing
        foreach ((string model, PricingConfig pricing) in DefaultPricing)
        {
            _cache.SetPricing(model, pricing);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CostMetrics CalculateCost(string model, in TokenMetrics tokens)
    {
        // Fast path for common models
        if (DefaultPricing.TryGetValue(model, out var defaultPricing))
        {
            return CalculateCostFast(defaultPricing, tokens);
        }

        // Fallback to cache lookup
        if (_cache.TryGetPricing(model, out var pricing))
        {
            return CalculateCostFast(pricing, tokens);
        }

        // Unknown model - use Claude 3.5 Sonnet as default
        _logger.LogWarning("Unknown Anthropic model {Model}, using default pricing", model);
        return CalculateCostFast(DefaultPricing["claude-3-5-sonnet-20241022"], tokens);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CostMetrics CalculateCostFast(PricingConfig pricing, in TokenMetrics tokens)
    {
        const decimal millionDivisor = 1_000_000m;
        
        var inputCost = tokens.InputTokens * pricing.InputPricePerMillion / millionDivisor;
        var outputCost = tokens.OutputTokens * pricing.OutputPricePerMillion / millionDivisor;
        var cacheCost = tokens.CachedTokens * pricing.CachePricePerMillion / millionDivisor;

        return new CostMetrics(inputCost, outputCost, cacheCost);
    }

    public void UpdatePricing(string model, PricingConfig config)
    {
        _cache.SetPricing(model, config);
        _logger.LogInformation("Updated pricing for Anthropic model {Model}", model);
    }
}

/// <summary>
/// No-cost calculator for OpenAI (only tracks tokens)
/// </summary>
public sealed class OpenAiPricingCalculator : IPricingCalculator
{
    public string ProviderName => "openai";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CostMetrics CalculateCost(string model, in TokenMetrics tokens)
    {
        // OpenAI format only tracks tokens, no cost calculation
        return new CostMetrics(0m, 0m, 0m);
    }

    public void UpdatePricing(string model, PricingConfig config)
    {
        // No-op for OpenAI as we don't calculate costs
    }
}

/// <summary>
/// Generic configurable pricing calculator
/// </summary>
public sealed class ConfigurablePricingCalculator(string providerName, ILogger<ConfigurablePricingCalculator> logger)
    : IPricingCalculator
{
    private readonly PricingCache _cache = new();

    public string ProviderName => providerName;

    public CostMetrics CalculateCost(string model, in TokenMetrics tokens)
    {
        if (!_cache.TryGetPricing(model, out var pricing))
        {
            logger.LogWarning("No pricing configuration found for {Provider} model {Model}", providerName, model);
            return new CostMetrics(0m, 0m, 0m);
        }

        return AnthropicPricingCalculator.CalculateCostFast(pricing, tokens);
    }

    public void UpdatePricing(string model, PricingConfig config)
    {
        _cache.SetPricing(model, config);
        logger.LogInformation("Updated pricing for {Provider} model {Model}", providerName, model);
    }

    // Extension method to make the private method accessible
    private static class AnthropicPricingCalculator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CostMetrics CalculateCostFast(PricingConfig pricing, in TokenMetrics tokens)
        {
            const decimal MillionDivisor = 1_000_000m;
            
            var inputCost = tokens.InputTokens * pricing.InputPricePerMillion / MillionDivisor;
            var outputCost = tokens.OutputTokens * pricing.OutputPricePerMillion / MillionDivisor;
            var cacheCost = tokens.CachedTokens * pricing.CachePricePerMillion / MillionDivisor;

            return new CostMetrics(inputCost, outputCost, cacheCost);
        }
    }
}

/// <summary>
/// Factory for creating pricing calculators
/// </summary>
public sealed class PricingCalculatorFactory(IServiceProvider serviceProvider, ILogger<PricingCalculatorFactory> logger)
{
    public IPricingCalculator CreateCalculator(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "anthropic" => serviceProvider.GetRequiredService<AnthropicPricingCalculator>(),
            "openai" => serviceProvider.GetRequiredService<OpenAiPricingCalculator>(),
            _ => CreateConfigurableCalculator(providerName)
        };
    }

    private IPricingCalculator CreateConfigurableCalculator(string providerName)
    {
        logger.LogInformation("Creating configurable pricing calculator for provider {Provider}", providerName);
        return new ConfigurablePricingCalculator(
            providerName, 
            serviceProvider.GetRequiredService<ILogger<ConfigurablePricingCalculator>>());
    }
}

/// <summary>
/// Pricing configuration manager with hot-reload support
/// </summary>
public sealed class PricingConfigurationManager : IDisposable
{
    private readonly IOptionsMonitor<Dictionary<string, Dictionary<string, PricingConfig>>> _options;
    private readonly Dictionary<string, IPricingCalculator> _calculators;
    private readonly IDisposable _configChangeSubscription;
    private readonly ILogger<PricingConfigurationManager> _logger;

    public PricingConfigurationManager(
        IOptionsMonitor<Dictionary<string, Dictionary<string, PricingConfig>>> options,
        IEnumerable<IPricingCalculator> calculators,
        ILogger<PricingConfigurationManager> logger)
    {
        _options = options;
        _calculators = calculators.ToDictionary(c => c.ProviderName, c => c, StringComparer.OrdinalIgnoreCase);
        _logger = logger;

        // Setup hot-reload of configuration
        _configChangeSubscription = _options.OnChange(OnConfigurationChanged);
        
        // Apply initial configuration
        OnConfigurationChanged(_options.CurrentValue);
    }

    private void OnConfigurationChanged(Dictionary<string, Dictionary<string, PricingConfig>> config)
    {
        foreach (var (providerName, models) in config)
        {
            if (!_calculators.TryGetValue(providerName, out var calculator))
            {
                _logger.LogWarning("No calculator found for provider {Provider}", providerName);
                continue;
            }

            foreach (var (model, pricing) in models)
            {
                calculator.UpdatePricing(model, pricing);
            }
        }

        _logger.LogInformation("Updated pricing configuration for {Count} providers", config.Count);
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
    }
}