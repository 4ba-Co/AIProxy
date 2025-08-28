using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class AnthropicPricingService : IAnthropicPricingService
{
    // Pricing per 1M tokens (USD) - Updated as of 2024
    private static readonly Dictionary<string, ModelPricing> _modelPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        // Claude 3.5 Sonnet
        ["claude-3-5-sonnet-20241022"] = new()
        {
            InputPrice = 3.00m,
            OutputPrice = 15.00m,
            CacheWritePrice = 3.75m,  // 25% markup for cache write
            CacheReadPrice = 0.30m    // 10% of input price for cache read
        },
        ["claude-3-5-sonnet-20240620"] = new()
        {
            InputPrice = 3.00m,
            OutputPrice = 15.00m,
            CacheWritePrice = 3.75m,
            CacheReadPrice = 0.30m
        },

        // Claude 3.5 Haiku
        ["claude-3-5-haiku-20241022"] = new()
        {
            InputPrice = 1.00m,
            OutputPrice = 5.00m,
            CacheWritePrice = 1.25m,
            CacheReadPrice = 0.10m
        },

        // Claude 3 Opus
        ["claude-3-opus-20240229"] = new()
        {
            InputPrice = 15.00m,
            OutputPrice = 75.00m,
            CacheWritePrice = 18.75m,
            CacheReadPrice = 1.50m
        },

        // Claude 3 Sonnet
        ["claude-3-sonnet-20240229"] = new()
        {
            InputPrice = 3.00m,
            OutputPrice = 15.00m,
            CacheWritePrice = 3.75m,
            CacheReadPrice = 0.30m
        },

        // Claude 3 Haiku
        ["claude-3-haiku-20240307"] = new()
        {
            InputPrice = 0.25m,
            OutputPrice = 1.25m,
            CacheWritePrice = 0.3125m,
            CacheReadPrice = 0.025m
        }
    };

    public TokenCostBreakdown CalculateTokenCosts(string model, AnthropicUsage usage)
    {
        if (!_modelPricing.TryGetValue(model, out var pricing))
        {
            // Default to Claude 3.5 Sonnet pricing if model not found
            pricing = _modelPricing["claude-3-5-sonnet-20241022"];
        }

        var breakdown = new TokenCostBreakdown
        {
            InputCost = CalculateCost(usage.InputTokens, pricing.InputPrice),
            OutputCost = CalculateCost(usage.OutputTokens, pricing.OutputPrice),
            CacheCreationCost = CalculateCost(usage.CacheCreationInputTokens ?? 0, pricing.CacheWritePrice),
            CacheReadCost = CalculateCost(usage.CacheReadInputTokens ?? 0, pricing.CacheReadPrice)
        };

        breakdown.TotalCost = breakdown.InputCost + breakdown.OutputCost +
                             breakdown.CacheCreationCost + breakdown.CacheReadCost;

        return breakdown;
    }

    private static decimal CalculateCost(int tokens, decimal pricePerMillion)
    {
        return tokens * pricePerMillion / 1_000_000m;
    }

    private sealed class ModelPricing
    {
        public required decimal InputPrice { get; init; }
        public required decimal OutputPrice { get; init; }
        public required decimal CacheWritePrice { get; init; }
        public required decimal CacheReadPrice { get; init; }
    }
}