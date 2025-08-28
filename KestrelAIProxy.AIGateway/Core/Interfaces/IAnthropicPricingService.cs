using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core.Interfaces;

public interface IAnthropicPricingService
{
    TokenCostBreakdown CalculateTokenCosts(string model, AnthropicUsage usage);
}