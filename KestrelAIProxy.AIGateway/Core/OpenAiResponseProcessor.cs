using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class OpenAiResponseProcessor : IResponseProcessor
{
    private readonly ILogger<OpenAiResponseProcessor> _logger;
    private readonly IMemoryEfficientStreamProcessor _streamProcessor;
    private readonly IEnumerable<ITokenParser> _tokenParsers;

    public OpenAiResponseProcessor(
        ILogger<OpenAiResponseProcessor> logger,
        IMemoryEfficientStreamProcessor streamProcessor,
        IEnumerable<ITokenParser> tokenParsers)
    {
        _logger = logger;
        _streamProcessor = streamProcessor;
        _tokenParsers = tokenParsers;
    }

    private ITokenParser GetOpenAiTokenParser()
    {
        return _tokenParsers.FirstOrDefault(p => p.GetType().Name.Contains("OpenAi"))
               ?? _tokenParsers.First();
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
            // Create a pipe for high-performance stream processing
            var pipe = new Pipe();
            var parser = GetOpenAiTokenParser();

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
            _logger.LogWarning(ex, "Failed to process OpenAI streaming response for request {RequestId}", requestId);
        }
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
            _logger.LogDebug(ex, "Stream reading interrupted");
        }
        finally
        {
            await writer.CompleteAsync();
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
                await _streamProcessor.ProcessStreamAsync(
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
            _logger.LogDebug(ex, "Pipe data processing interrupted");
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
        var openAiUsage = new OpenAiUsage
        {
            PromptTokens = tokens.InputTokens,
            CompletionTokens = tokens.OutputTokens,
            TotalTokens = tokens.TotalTokens
        };

        var usageResult = new OpenAiUsageResult
        {
            RequestId = requestId,
            Provider = provider,
            Model = "unknown", // Will be updated when model info is available
            Timestamp = DateTime.UtcNow,
            IsStreaming = true,
            Usage = openAiUsage
        };

        await onUsageDetected(usageResult);
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

                await ProcessStreamingChunk(jsonData, requestId, provider, onUsageDetected);
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
                    await ProcessStreamingChunk(jsonData, requestId, provider, onUsageDetected);
                }
            }
        }
    }

    private async Task ProcessStreamingChunk(
        string jsonData,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected)
    {
        try
        {
            var chunk = JsonSerializer.Deserialize(jsonData, SourceGenerationContext.Default.OpenAiStreamChunk);

            if (chunk?.Usage != null)
            {
                var usageResult = new OpenAiUsageResult
                {
                    RequestId = requestId,
                    Provider = provider,
                    Model = chunk.Model ?? "unknown",
                    IsStreaming = true,
                    Usage = chunk.Usage
                };

                await onUsageDetected(usageResult);
            }
        }
        catch (JsonException)
        {
            _logger.LogTrace("Invalid OpenAI SSE chunk: {JsonData}", jsonData.Length > 100 ? jsonData[..100] + "..." : jsonData);
        }
    }

    private async Task ProcessNonStreamingResponse(
        Stream responseStream,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        var responseText = string.Empty;

        try
        {
            // Copy all response data efficiently
            await responseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            // Convert stream to string with proper encoding
            if (memoryStream.Length == 0) return;

            using var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            responseText = await reader.ReadToEndAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(responseText)) return;

            _logger.LogTrace("Processing OpenAI response of length {Length} for request {RequestId}",
                responseText.Length, requestId);

            var response = JsonSerializer.Deserialize(responseText, SourceGenerationContext.Default.OpenAiResponse);

            if (response?.Usage != null)
            {
                var usageResult = new OpenAiUsageResult
                {
                    RequestId = requestId,
                    Provider = provider,
                    Model = response.Model ?? "unknown",
                    IsStreaming = false,
                    Usage = response.Usage,
                    Timestamp = DateTime.UtcNow
                };

                await onUsageDetected(usageResult);
                _logger.LogInformation("OpenAI usage: {RequestId} - Input:{PromptTokens}/Output:{CompletionTokens}/Total:{TotalTokens}",
                    requestId, response.Usage.PromptTokens, response.Usage.CompletionTokens, response.Usage.TotalTokens);
            }
            else
            {
                _logger.LogTrace("No usage data in OpenAI response for {RequestId}", requestId);
            }
        }
        catch (JsonException)
        {
            _logger.LogDebug("Failed to parse OpenAI JSON for {RequestId}, length: {Length}",
                requestId, responseText.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process OpenAI response for {RequestId}", requestId);
        }
    }
}