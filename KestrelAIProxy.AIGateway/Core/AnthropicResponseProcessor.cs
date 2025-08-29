using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class AnthropicResponseProcessor(
    ILogger<AnthropicResponseProcessor> logger,
    IAnthropicPricingService pricingService,
    IMemoryEfficientStreamProcessor streamProcessor,
    IEnumerable<ITokenParser> tokenParsers)
    : IResponseProcessor
{

    private ITokenParser GetAnthropicTokenParser()
    {
        return tokenParsers.FirstOrDefault(p => p.GetType().Name.Contains("Anthropic"))
               ?? tokenParsers.First();
    }

    public async Task ProcessAsync(
        Stream responseStream,
        string requestId,
        bool isStreaming,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken = default)
    {
        if (isStreaming)
        {
            await ProcessStreamingResponse(responseStream, requestId, provider, onUsageDetected, cancellationToken);
        }
        else
        {
            await ProcessNonStreamingResponse(responseStream, requestId, provider, onUsageDetected, cancellationToken);
        }
    }

    private async Task ProcessStreamingResponse(
        Stream responseStream,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create a pipe for high-performance stream processing (consistent with OpenAI)
            var pipe = new Pipe();
            var parser = GetAnthropicTokenParser();

            // Start reading from the stream into the pipe
            var readTask = ReadStreamIntoPipeAsync(responseStream, pipe.Writer, cancellationToken);

            // Process the pipe data with the high-performance processor
            var processTask = ProcessPipeDataAsync(pipe.Reader, parser, requestId, provider, onUsageDetected, cancellationToken);

            // Wait for both tasks to complete
            await Task.WhenAll(readTask, processTask);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process Anthropic streaming response for {RequestId}", requestId);
        }
    }

    private async Task ProcessPipeDataAsync(
        PipeReader reader,
        ITokenParser parser,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.IsEmpty && result.IsCompleted)
                    break;

                // Process the buffer with high-performance stream processor
                await streamProcessor.ProcessStreamAsync(
                    buffer,
                    parser,
                    async tokens => await OnTokensDetected(tokens, requestId, provider, onUsageDetected),
                    cancellationToken);

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Pipe data processing interrupted");
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    private async ValueTask OnTokensDetected(
        TokenMetrics tokens,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected)
    {
        var anthropicUsage = new AnthropicUsage
        {
            InputTokens = tokens.InputTokens,
            OutputTokens = tokens.OutputTokens
        };

        // Calculate costs using pricing service
        var costBreakdown = pricingService.CalculateTokenCosts("unknown", anthropicUsage);

        var usageResult = new AnthropicUsageResult
        {
            RequestId = requestId,
            Provider = provider,
            Model = "unknown", // Will be updated when model info is available
            Timestamp = DateTime.UtcNow,
            IsStreaming = true,
            Usage = anthropicUsage,
            TotalCost = costBreakdown.TotalCost,
            CostBreakdown = costBreakdown
        };

        await onUsageDetected(usageResult);
    }

    private async Task ReadStreamIntoPipeAsync(Stream stream, PipeWriter writer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var memory = writer.GetMemory();
                var bytesRead = await stream.ReadAsync(memory, cancellationToken);

                if (bytesRead == 0)
                    break;

                writer.Advance(bytesRead);
                await writer.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Stream reading interrupted");
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    private async Task ProcessStreamingLines(
        StringBuilder lineBuffer,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        bool flush = false)
    {
        string content = lineBuffer.ToString();

        int newlineIndex;
        while ((newlineIndex = content.IndexOf('\n')) != -1)
        {
            var line = content[..newlineIndex].TrimEnd('\r');
            content = content[(newlineIndex + 1)..];

            if (line.StartsWith("data: "))
            {
                var jsonData = line[6..].Trim();
                if (jsonData == "[DONE]") continue;

                await ProcessStreamingEvent(jsonData, requestId, provider, onUsageDetected);
            }
        }

        // Update buffer with remaining content
        lineBuffer.Clear();
        if (!flush && content.Length > 0)
        {
            lineBuffer.Append(content);
        }
        else if (flush && content.Length > 0)
        {
            // Process final chunk if it doesn't end with newline
            if (content.TrimStart().StartsWith("data: "))
            {
                var jsonData = content.TrimStart()[6..].Trim();
                if (jsonData != "[DONE]")
                {
                    await ProcessStreamingEvent(jsonData, requestId, provider, onUsageDetected);
                }
            }
        }
    }

    private async Task ProcessStreamingEvent(
        string jsonData,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected)
    {
        try
        {
            var streamEvent = JsonSerializer.Deserialize(jsonData, SourceGenerationContext.Default.AnthropicStreamEvent);

            if (streamEvent?.Type == "message_stop" && streamEvent.Usage != null)
            {
                await ProcessAnthropicUsage(streamEvent.Usage, streamEvent.Message?.Model ?? "unknown",
                    requestId, provider, true, onUsageDetected);
            }
            else if (streamEvent?.Type == "message_start" && streamEvent.Message?.Usage != null)
            {
                await ProcessAnthropicUsage(streamEvent.Message.Usage, streamEvent.Message.Model ?? "unknown",
                    requestId, provider, true, onUsageDetected);
            }
        }
        catch (JsonException)
        {
            logger.LogTrace("Invalid Anthropic SSE event: {JsonData}", jsonData.Length > 100 ? jsonData[..100] + "..." : jsonData);
        }
    }

    private async Task ProcessNonStreamingResponse(
        Stream responseStream,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use the same Pipe-based approach as streaming for consistency
            var pipe = new Pipe();
            var parser = GetAnthropicTokenParser();

            // Read stream data into pipe
            var readTask = ReadStreamIntoPipeAsync(responseStream, pipe.Writer, cancellationToken);

            // Collect all data from pipe into a single buffer for non-streaming JSON parsing
            var collectTask = CollectNonStreamingDataAsync(pipe.Reader, parser, requestId, provider, onUsageDetected, cancellationToken);

            // Wait for both tasks to complete
            await Task.WhenAll(readTask, collectTask);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process Anthropic non-streaming response for request {RequestId}", requestId);
        }
    }

    private async Task CollectNonStreamingDataAsync(
        PipeReader reader,
        ITokenParser parser,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken)
    {
        var responseData = new List<ReadOnlyMemory<byte>>();
        var totalLength = 0;

        try
        {
            // Collect all data from the pipe
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (!buffer.IsEmpty)
                {
                    // Store the buffer data
                    foreach (var segment in buffer)
                    {
                        var copy = new byte[segment.Length];
                        segment.CopyTo(copy);
                        responseData.Add(copy);
                        totalLength += segment.Length;
                    }
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                    break;
            }

            // Process the collected data
            if (totalLength > 0)
            {
                await ProcessCollectedAnthropicJsonData(responseData, totalLength, requestId, provider, onUsageDetected, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Non-streaming data collection interrupted");
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }

    private async Task ProcessCollectedAnthropicJsonData(
        List<ReadOnlyMemory<byte>> responseData,
        int totalLength,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken)
    {
        // Combine all data into a single contiguous buffer
        var combinedBuffer = new byte[totalLength];
        var offset = 0;

        foreach (var segment in responseData)
        {
            segment.CopyTo(combinedBuffer.AsMemory(offset));
            offset += segment.Length;
        }

        var responseText = string.Empty;

        try
        {
            // Convert to string with proper UTF-8 handling
            responseText = Encoding.UTF8.GetString(combinedBuffer);

            if (string.IsNullOrWhiteSpace(responseText)) return;

            logger.LogTrace("Processing Anthropic response of length {Length} for request {RequestId}",
                responseText.Length, requestId);

            var response = JsonSerializer.Deserialize(responseText, SourceGenerationContext.Default.AnthropicResponse);

            if (response?.Usage != null)
            {
                await ProcessAnthropicUsage(response.Usage, response.Model ?? "unknown",
                    requestId, provider, false, onUsageDetected);
            }
            else
            {
                logger.LogTrace("No usage data in Anthropic response for {RequestId}", requestId);
            }
        }
        catch (JsonException)
        {
            logger.LogDebug("Failed to parse Anthropic JSON for {RequestId}, length: {Length}",
                requestId, responseText.Length);
        }
    }

    private async Task ProcessAnthropicUsage(
        AnthropicUsage usage,
        string model,
        string requestId,
        string provider,
        bool isStreaming,
        Func<BaseUsageResult, Task> onUsageDetected)
    {
        try
        {
            var costBreakdown = pricingService.CalculateTokenCosts(model, usage);

            var usageResult = new AnthropicUsageResult
            {
                RequestId = requestId,
                Provider = provider,
                Model = model,
                IsStreaming = isStreaming,
                Usage = usage,
                TotalCost = costBreakdown.TotalCost,
                CostBreakdown = costBreakdown
            };

            await onUsageDetected(usageResult);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process Anthropic usage for {RequestId}", requestId);
        }
    }
}