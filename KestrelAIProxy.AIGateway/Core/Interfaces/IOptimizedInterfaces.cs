using System.Buffers;
using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core.Interfaces;

/// <summary>
/// High-performance token parser with minimal allocations
/// </summary>
public interface ITokenParser
{
    /// <summary>
    /// Parse token usage from JSON data with minimal allocations
    /// </summary>
    bool TryParseTokens(ReadOnlySpan<byte> jsonData, out TokenMetrics tokens);
    
    /// <summary>
    /// Parse token usage from streaming chunk
    /// </summary>
    bool TryParseStreamingTokens(ReadOnlySpan<byte> chunkData, out TokenMetrics tokens);
}

/// <summary>
/// Pluggable pricing calculator interface
/// </summary>
public interface IPricingCalculator
{
    /// <summary>
    /// Provider name this calculator supports
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Calculate costs for token usage
    /// </summary>
    CostMetrics CalculateCost(string model, in TokenMetrics tokens);
    
    /// <summary>
    /// Update pricing configuration
    /// </summary>
    void UpdatePricing(string model, PricingConfig config);
}

/// <summary>
/// High-performance usage processor with batching
/// </summary>
public interface IUsageProcessor
{
    /// <summary>
    /// Process a single usage record asynchronously
    /// </summary>
    ValueTask ProcessAsync(UsageRecord record, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process a batch of usage records
    /// </summary>
    ValueTask ProcessBatchAsync(UsageBatch batch, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Flush any pending records
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Configurable usage sink for different storage backends
/// </summary>
public interface IUsageSink
{
    /// <summary>
    /// Write usage records to the sink
    /// </summary>
    ValueTask WriteAsync(ReadOnlyMemory<UsageRecord> records, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Flush any buffered data
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Memory pool aware stream processor
/// </summary>
public interface IMemoryEfficientStreamProcessor
{
    /// <summary>
    /// Process stream data using memory pools
    /// </summary>
    ValueTask ProcessStreamAsync(
        ReadOnlySequence<byte> data,
        ITokenParser parser,
        Func<TokenMetrics, ValueTask> onTokensParsed,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sampling strategy for usage tracking
/// </summary>
public interface ISamplingStrategy
{
    /// <summary>
    /// Determine if a request should be tracked
    /// </summary>
    bool ShouldTrack(string requestId, string provider, string model);
    
    /// <summary>
    /// Update sampling configuration
    /// </summary>
    void UpdateSamplingRate(double rate);
}

/// <summary>
/// Circuit breaker for usage tracking
/// </summary>
public interface IUsageTrackingCircuitBreaker
{
    /// <summary>
    /// Check if usage tracking is currently allowed
    /// </summary>
    bool IsAllowed { get; }
    
    /// <summary>
    /// Record a successful operation
    /// </summary>
    void RecordSuccess();
    
    /// <summary>
    /// Record a failed operation
    /// </summary>
    void RecordFailure();
    
    /// <summary>
    /// Get current circuit state
    /// </summary>
    CircuitBreakerState State { get; }
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitBreakerState
{
    Closed,    // Normal operation
    Open,      // Circuit is open, requests are being rejected
    HalfOpen   // Testing if the circuit can be closed
}