using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using KestrelAIProxy.AIGateway.Core.Infrastructure;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace KestrelAIProxy.AIGateway.Core.Processing;

/// <summary>
/// Optimized stream processor using System.IO.Pipelines and memory pools for token usage extraction
/// </summary>
public sealed class TokenUsageStreamProcessor : IMemoryEfficientStreamProcessor, IDisposable
{
    private readonly ILogger<TokenUsageStreamProcessor> _logger;
    private readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;

    // Pre-compiled patterns for faster SSE parsing
    private static ReadOnlySpan<byte> DataPrefix => "data: "u8;
    private static ReadOnlySpan<byte> DoneMarker => "[DONE]"u8;
    private static ReadOnlySpan<byte> NewLine => "\n"u8;

    public TokenUsageStreamProcessor(ILogger<TokenUsageStreamProcessor> logger)
    {
        _logger = logger;
        _stringBuilderPool = new DefaultObjectPool<StringBuilder>(new Infrastructure.StringBuilderPooledObjectPolicy());
    }

    public async ValueTask ProcessStreamAsync(
        ReadOnlySequence<byte> data,
        ITokenParser parser,
        Func<TokenMetrics, ValueTask> onTokensParsed,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ProcessSequenceAsync(data, parser, onTokensParsed, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stream sequence");
        }
    }

    private async ValueTask ProcessSequenceAsync(
        ReadOnlySequence<byte> sequence,
        ITokenParser parser,
        Func<TokenMetrics, ValueTask> onTokensParsed,
        CancellationToken cancellationToken)
    {
        var reader = new SequenceReader<byte>(sequence);
        var linesToProcess = new List<ReadOnlySequence<byte>>();
        
        // First, extract all lines synchronously
        while (!reader.End && !cancellationToken.IsCancellationRequested)
        {
            if (TryReadSseLine(ref reader, out var lineData))
            {
                linesToProcess.Add(lineData);
            }
        }
        
        // Then process them asynchronously
        foreach (var lineData in linesToProcess)
        {
            await ProcessSseLineAsync(lineData, parser, onTokensParsed);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadSseLine(ref SequenceReader<byte> reader, out ReadOnlySequence<byte> line)
    {
        if (reader.TryReadTo(out line, NewLine, advancePastDelimiter: true))
        {
            return true;
        }

        // Handle case where there's data but no newline (end of stream)
        if (!reader.End)
        {
            line = reader.Sequence.Slice(reader.Position);
            reader.Advance(line.Length);
            return line.Length > 0;
        }

        line = default;
        return false;
    }

    private async ValueTask ProcessSseLineAsync(
        ReadOnlySequence<byte> lineData,
        ITokenParser parser,
        Func<TokenMetrics, ValueTask> onTokensParsed)
    {
        // Skip empty lines
        if (lineData.Length == 0) return;

        // Check if it's a data line
        if (!StartsWithDataPrefix(lineData)) return;

        // Extract JSON payload (skip "data: " prefix)
        var jsonData = lineData.Slice(DataPrefix.Length);
        
        // Skip [DONE] markers
        if (jsonData.Length >= DoneMarker.Length && 
            jsonData.FirstSpan.StartsWith(DoneMarker))
        {
            return;
        }

        // Parse tokens from JSON
        if (TryGetSpanFromSequence(jsonData, out var jsonSpan))
        {
            if (parser.TryParseStreamingTokens(jsonSpan, out var tokens))
            {
                await onTokensParsed(tokens);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWithDataPrefix(ReadOnlySequence<byte> sequence)
    {
        if (sequence.Length < DataPrefix.Length) return false;
        
        var firstSpan = sequence.FirstSpan;
        if (firstSpan.Length >= DataPrefix.Length)
        {
            return firstSpan.StartsWith(DataPrefix);
        }

        // Handle case where prefix spans multiple segments
        Span<byte> buffer = stackalloc byte[DataPrefix.Length];
        sequence.Slice(0, DataPrefix.Length).CopyTo(buffer);
        return buffer.SequenceEqual(DataPrefix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetSpanFromSequence(ReadOnlySequence<byte> sequence, out ReadOnlySpan<byte> span)
    {
        if (sequence.IsSingleSegment)
        {
            span = sequence.FirstSpan;
            return true;
        }

        // For multi-segment sequences, we need to copy to a contiguous buffer
        if (sequence.Length > 4096) // Reasonable limit for JSON payloads
        {
            span = default;
            return false;
        }

        var buffer = _bytePool.Rent((int)sequence.Length);
        try
        {
            sequence.CopyTo(buffer);
            span = buffer.AsSpan(0, (int)sequence.Length);
            return true;
        }
        finally
        {
            _bytePool.Return(buffer);
        }
    }

    public void Dispose()
    {
        // Nothing to dispose for pools as they're shared
    }
}

/// <summary>
/// Optimized JSON parser for token usage data
/// </summary>
public abstract class OptimizedTokenParser : ITokenParser
{
    protected static readonly JsonReaderOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public abstract bool TryParseTokens(ReadOnlySpan<byte> jsonData, out TokenMetrics tokens);
    public abstract bool TryParseStreamingTokens(ReadOnlySpan<byte> chunkData, out TokenMetrics tokens);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool TryGetProperty(ref Utf8JsonReader reader, ReadOnlySpan<byte> propertyName, out JsonElement element)
    {
        element = default;
        
        if (reader.TokenType != JsonTokenType.PropertyName || 
            !reader.ValueTextEquals(propertyName))
        {
            return false;
        }

        reader.Read();
        using var doc = JsonDocument.ParseValue(ref reader);
        element = doc.RootElement.Clone();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool TryReadInt32(ref Utf8JsonReader reader, out int value)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}

/// <summary>
/// High-performance OpenAI token parser
/// </summary>
public sealed class OpenAiTokenParser : OptimizedTokenParser
{
    public override bool TryParseTokens(ReadOnlySpan<byte> jsonData, out TokenMetrics tokens)
    {
        tokens = default;
        
        try
        {
            var reader = new Utf8JsonReader(jsonData, JsonOptions);
            return TryParseOpenAiUsage(ref reader, out tokens);
        }
        catch
        {
            return false;
        }
    }

    public override bool TryParseStreamingTokens(ReadOnlySpan<byte> chunkData, out TokenMetrics tokens)
    {
        return TryParseTokens(chunkData, out tokens);
    }

    private static bool TryParseOpenAiUsage(ref Utf8JsonReader reader, out TokenMetrics tokens)
    {
        tokens = default;
        int promptTokens = 0, completionTokens = 0, cachedTokens = 0;

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            return false;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (reader.ValueTextEquals("usage"u8))
            {
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartObject)
                    continue;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals("prompt_tokens"u8))
                        {
                            reader.Read();
                            TryReadInt32(ref reader, out promptTokens);
                        }
                        else if (reader.ValueTextEquals("completion_tokens"u8))
                        {
                            reader.Read();
                            TryReadInt32(ref reader, out completionTokens);
                        }
                        else if (reader.ValueTextEquals("prompt_tokens_details"u8))
                        {
                            reader.Read();
                            if (reader.TokenType == JsonTokenType.StartObject)
                            {
                                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                {
                                    if (reader.TokenType == JsonTokenType.PropertyName &&
                                        reader.ValueTextEquals("cached_tokens"u8))
                                    {
                                        reader.Read();
                                        TryReadInt32(ref reader, out cachedTokens);
                                    }
                                    else
                                    {
                                        reader.Skip();
                                    }
                                }
                            }
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                }

                tokens = new TokenMetrics(promptTokens, completionTokens, cachedTokens);
                return true;
            }
            else
            {
                reader.Skip();
            }
        }

        return false;
    }
}

/// <summary>
/// High-performance Anthropic token parser
/// </summary>
public sealed class AnthropicTokenParser : OptimizedTokenParser
{
    public override bool TryParseTokens(ReadOnlySpan<byte> jsonData, out TokenMetrics tokens)
    {
        tokens = default;
        
        try
        {
            var reader = new Utf8JsonReader(jsonData, JsonOptions);
            return TryParseAnthropicUsage(ref reader, out tokens);
        }
        catch
        {
            return false;
        }
    }

    public override bool TryParseStreamingTokens(ReadOnlySpan<byte> chunkData, out TokenMetrics tokens)
    {
        return TryParseTokens(chunkData, out tokens);
    }

    private static bool TryParseAnthropicUsage(ref Utf8JsonReader reader, out TokenMetrics tokens)
    {
        tokens = default;
        int inputTokens = 0, outputTokens = 0, cacheTokens = 0;

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            return false;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (reader.ValueTextEquals("usage"u8))
            {
                reader.Read();
                if (reader.TokenType != JsonTokenType.StartObject)
                    continue;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals("input_tokens"u8))
                        {
                            reader.Read();
                            TryReadInt32(ref reader, out inputTokens);
                        }
                        else if (reader.ValueTextEquals("output_tokens"u8))
                        {
                            reader.Read();
                            TryReadInt32(ref reader, out outputTokens);
                        }
                        else if (reader.ValueTextEquals("cache_creation_input_tokens"u8) ||
                                 reader.ValueTextEquals("cache_read_input_tokens"u8))
                        {
                            reader.Read();
                            if (TryReadInt32(ref reader, out int cacheValue))
                                cacheTokens += cacheValue;
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                }

                tokens = new TokenMetrics(inputTokens, outputTokens, cacheTokens);
                return true;
            }
            else if (reader.ValueTextEquals("message"u8))
            {
                reader.Read();
                reader.Skip(); // We handle usage at the top level or in dedicated usage objects
            }
            else
            {
                reader.Skip();
            }
        }

        return false;
    }
}

