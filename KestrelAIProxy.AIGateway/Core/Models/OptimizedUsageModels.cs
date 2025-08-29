using System.Runtime.CompilerServices;

namespace KestrelAIProxy.AIGateway.Core.Models;

/// <summary>
/// High-performance token usage data with minimal allocations
/// </summary>
public readonly struct TokenMetrics(int inputTokens, int outputTokens, int cachedTokens = 0)
{
    public readonly int InputTokens = inputTokens;
    public readonly int OutputTokens = outputTokens;
    public readonly int CachedTokens = cachedTokens;
    public readonly int TotalTokens = inputTokens + outputTokens;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TokenMetrics Add(in TokenMetrics other) => new(
        InputTokens + other.InputTokens,
        OutputTokens + other.OutputTokens,
        CachedTokens + other.CachedTokens);

    public override string ToString() =>
        $"I:{InputTokens}/O:{OutputTokens}/C:{CachedTokens}/T:{TotalTokens}";
}

/// <summary>
/// Cost breakdown with optimized decimal operations
/// </summary>
public readonly struct CostMetrics(decimal inputCost, decimal outputCost, decimal cacheCost = 0m)
{
    public readonly decimal InputCost = inputCost;
    public readonly decimal OutputCost = outputCost;
    public readonly decimal CacheCost = cacheCost;
    public readonly decimal TotalCost = inputCost + outputCost + cacheCost;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CostMetrics Add(in CostMetrics other) => new(
        InputCost + other.InputCost,
        OutputCost + other.OutputCost,
        CacheCost + other.CacheCost);

    public override string ToString() =>
        $"I:${InputCost:F6}/O:${OutputCost:F6}/C:${CacheCost:F6}/T:${TotalCost:F6}";
}