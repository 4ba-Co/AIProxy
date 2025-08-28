using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class AnthropicResponseProcessor : IResponseProcessor
{
    private readonly ILogger<AnthropicResponseProcessor> _logger;
    private readonly IAnthropicPricingService _pricingService;
    private readonly IMemoryEfficientStreamProcessor _streamProcessor;
    private readonly IEnumerable<ITokenParser> _tokenParsers;

    public AnthropicResponseProcessor(
        ILogger<AnthropicResponseProcessor> logger,
        IAnthropicPricingService pricingService,
        IMemoryEfficientStreamProcessor streamProcessor,
        IEnumerable<ITokenParser> tokenParsers)
    {
        _logger = logger;
        _pricingService = pricingService;
        _streamProcessor = streamProcessor;
        _tokenParsers = tokenParsers;
    }

    private ITokenParser GetAnthropicTokenParser()
    {
        return _tokenParsers.FirstOrDefault(p => p.GetType().Name.Contains("Anthropic"))
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
        var buffer = new byte[8192];
        var lineBuffer = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break;

                var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                lineBuffer.Append(text);

                await ProcessStreamingLines(lineBuffer, requestId, provider, onUsageDetected);
            }

            // Process any remaining content
            if (lineBuffer.Length > 0)
            {
                await ProcessStreamingLines(lineBuffer, requestId, provider, onUsageDetected, true);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Anthropic streaming response for {RequestId}", requestId);
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
            _logger.LogTrace("Invalid Anthropic SSE event: {JsonData}", jsonData.Length > 100 ? jsonData[..100] + "..." : jsonData);
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
            using var reader = new StreamReader(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var responseText = await reader.ReadToEndAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(responseText)) return;

            var response = JsonSerializer.Deserialize(responseText, SourceGenerationContext.Default.AnthropicResponse);

            if (response?.Usage != null)
            {
                await ProcessAnthropicUsage(response.Usage, response.Model ?? "unknown",
                    requestId, provider, false, onUsageDetected);
            }
        }
        catch (JsonException)
        {
            _logger.LogDebug("Failed to parse Anthropic JSON for {RequestId}", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Anthropic response for {RequestId}", requestId);
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
            var costBreakdown = _pricingService.CalculateTokenCosts(model, usage);

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
            _logger.LogWarning(ex, "Failed to process Anthropic usage for {RequestId}", requestId);
        }
    }
}