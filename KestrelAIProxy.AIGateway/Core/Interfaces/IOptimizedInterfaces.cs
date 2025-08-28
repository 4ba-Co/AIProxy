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