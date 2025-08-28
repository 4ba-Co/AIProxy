using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using KestrelAIProxy.AIGateway.Core.Infrastructure;
using Microsoft.Extensions.ObjectPool;

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

/// <summary>
/// Optimized usage record with value semantics and memory pooling
/// </summary>
public sealed class UsageRecord : IDisposable, IPoolResettable
{
    private static readonly ObjectPool<UsageRecord> Pool = new FastObjectPool<UsageRecord>(
        new Infrastructure.DefaultPooledObjectPolicy<UsageRecord>());

    public string RequestId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsStreaming { get; set; }
    public DateTime Timestamp { get; set; }
    public TokenMetrics Tokens { get; set; }
    public CostMetrics? Costs { get; set; }

    // Additional metadata with pooled arrays
    private KeyValuePair<string, object>[]? _metadata;
    private int _metadataCount;

    public static UsageRecord Rent() => Pool.Get();

    void IPoolResettable.Reset()
    {
        RequestId = string.Empty;
        Provider = string.Empty;
        Model = string.Empty;
        IsStreaming = false;
        Timestamp = default;
        Tokens = default;
        Costs = null;
        _metadataCount = 0;
    }

    public void AddMetadata(string key, object value)
    {
        _metadata ??= ArrayPool<KeyValuePair<string, object>>.Shared.Rent(8);
        
        if (_metadataCount < _metadata.Length)
        {
            _metadata[_metadataCount] = new(key, value);
            _metadataCount++;
        }
    }

    public ReadOnlySpan<KeyValuePair<string, object>> GetMetadata() =>
        _metadata != null ? _metadata.AsSpan(0, _metadataCount) : ReadOnlySpan<KeyValuePair<string, object>>.Empty;

    public void Dispose()
    {
        if (_metadata != null)
        {
            ArrayPool<KeyValuePair<string, object>>.Shared.Return(_metadata, true);
            _metadata = null;
        }
        Pool.Return(this);
    }
}

/// <summary>
/// Batch processing container for usage records
/// </summary>
public sealed class UsageBatch : IDisposable, IPoolResettable
{
    private readonly List<UsageRecord> _records = new(32);
    private static readonly ObjectPool<UsageBatch> Pool = new FastObjectPool<UsageBatch>(
        new Infrastructure.DefaultPooledObjectPolicy<UsageBatch>());

    public int Count => _records.Count;
    public IReadOnlyList<UsageRecord> Records => _records.AsReadOnly();

    public static UsageBatch Rent() => Pool.Get();

    public void AddRecord(UsageRecord record) => _records.Add(record);

    public void Clear()
    {
        foreach (var record in _records)
        {
            record.Dispose();
        }
        _records.Clear();
    }

    void IPoolResettable.Reset()
    {
        Clear();
    }

    public void Dispose()
    {
        Clear();
        Pool.Return(this);
    }
}

/// <summary>
/// Configuration for usage tracking behavior
/// </summary>
public sealed class UsageTrackingOptions
{
    /// <summary>
    /// Whether to enable usage tracking
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Batch size for processing usage records
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Flush interval for batched processing (milliseconds)
    /// </summary>
    public int FlushIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Maximum queue size before dropping records
    /// </summary>
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>
    /// Whether to enable detailed cost calculation
    /// </summary>
    public bool EnableCostCalculation { get; set; } = true;

    /// <summary>
    /// Whether to track detailed metadata
    /// </summary>
    public bool EnableMetadataTracking { get; set; } = false;

    /// <summary>
    /// Sampling rate (0.0 to 1.0) for usage tracking
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;
}

/// <summary>
/// Provider-specific pricing configuration
/// </summary>
public sealed class PricingConfig
{
    public decimal InputPricePerMillion { get; set; }
    public decimal OutputPricePerMillion { get; set; }
    public decimal CachePricePerMillion { get; set; }
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// Fast lookup cache for pricing information
/// </summary>
public sealed class PricingCache
{
    private readonly ConcurrentDictionary<string, PricingConfig> _cache = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public bool TryGetPricing(string model, out PricingConfig config) =>
        _cache.TryGetValue(model, out config!);

    public void SetPricing(string model, PricingConfig config) =>
        _cache[model] = config;

    public void Clear() => _cache.Clear();
}

/// <summary>
/// Memory-efficient JSON parsing context for usage data
/// </summary>
public readonly ref struct JsonParseContext
{
    public readonly ReadOnlySpan<byte> Data;
    public readonly JsonDocumentOptions Options;

    public JsonParseContext(ReadOnlySpan<byte> data, JsonDocumentOptions options = default)
    {
        Data = data;
        Options = options;
    }
}