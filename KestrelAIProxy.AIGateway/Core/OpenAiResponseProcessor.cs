using System.Text;
using System.Text.Json;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class OpenAiResponseProcessor : IResponseProcessor
{
    private readonly ILogger<OpenAiResponseProcessor> _logger;

    public OpenAiResponseProcessor(ILogger<OpenAiResponseProcessor> logger)
    {
        _logger = logger;
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
            _logger.LogError(ex, "Error processing OpenAI streaming response for request {RequestId}", requestId);
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
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenAI streaming chunk JSON: {JsonData}", jsonData);
        }
    }

    private async Task ProcessNonStreamingResponse(
        Stream responseStream,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var responseBuffer = new List<byte>();
        string responseText = string.Empty;

        try
        {
            // Read all response data while preserving it
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break;

                // Add to our collection for analysis
                responseBuffer.AddRange(buffer.Take(bytesRead));
            }

            // Convert byte array to string with proper BOM handling
            var responseBytes = responseBuffer.ToArray();
            if (responseBytes.Length == 0) return;
            // FIXME: encoding error
            // Check for and skip UTF-8 BOM if present
            var startIndex = 0;
            if (responseBytes.Length >= 3 && 
                responseBytes[0] == 0xEF && 
                responseBytes[1] == 0xBB && 
                responseBytes[2] == 0xBF)
            {
                startIndex = 3;
            }
            
            responseText = Encoding.UTF8.GetString(responseBytes, startIndex, responseBytes.Length - startIndex);
            
            if (string.IsNullOrWhiteSpace(responseText)) return;

            _logger.LogDebug("Processing OpenAI response of length {Length} for request {RequestId}", 
                responseText.Length, requestId);

            // Log first few characters for debugging
            _logger.LogDebug("First 50 chars of response: {FirstChars}", 
                responseText.Length > 50 ? responseText[..50] : responseText);

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
                _logger.LogDebug("OpenAI usage extracted for request {RequestId}: {PromptTokens}/{CompletionTokens}/{TotalTokens}", 
                    requestId, response.Usage.PromptTokens, response.Usage.CompletionTokens, response.Usage.TotalTokens);
            }
            else
            {
                _logger.LogDebug("No usage information found in OpenAI response for request {RequestId}", requestId);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenAI response JSON for request {RequestId}. Response length: {Length}", 
                requestId, responseText.Length);
            _logger.LogDebug("JSON parsing failed for content: {Content}", 
                responseText.Length > 500 ? responseText[..500] : responseText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OpenAI non-streaming response for request {RequestId}", requestId);
        }
    }
}