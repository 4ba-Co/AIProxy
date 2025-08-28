using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
using KestrelAIProxy.AIGateway.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class OpenAiUsageTracker : IUsageTracker
{
    private readonly ILogger<OpenAiUsageTracker> _logger;
    private static readonly string[] SupportedEndpoints = {
        "v1/chat/completions",
        "v1/completions",
        "v1/embeddings"
    };

    public string ProviderName => "openai";

    public OpenAiUsageTracker(ILogger<OpenAiUsageTracker> logger)
    {
        _logger = logger;
    }

    public bool ShouldTrack(HttpContext context, ParsedPath parsedPath)
    {
        // Check if this is an OpenAI request by provider name from parsed path
        if (!string.Equals(parsedPath.ProviderName, ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check if this is a supported endpoint
        var segments = parsedPath.ProviderSegments;
        if (segments.Length == 0)
        {
            return false;
        }

        // Build the endpoint path from segments
        var endpointPath = string.Join("/", segments);
        return SupportedEndpoints.Any(endpoint =>
            endpoint.Equals(endpointPath, StringComparison.OrdinalIgnoreCase));
    }

    public async Task OnUsageDetectedAsync(BaseUsageResult usageResult)
    {
        if (usageResult is not OpenAiUsageResult openAiUsage)
        {
            _logger.LogWarning("Invalid usage result type for OpenAI tracker: {Type}", usageResult.GetType().Name);
            return;
        }

        try
        {
            // Log the usage - only token counts, no billing for OpenAI format
            _logger.LogInformation(
                "OpenAI API Usage - Request: {RequestId}, Model: {Model}, " +
                "Tokens: Prompt={PromptTokens}/Completion={CompletionTokens}/Total={TotalTokens}, " +
                "Details: Cached={CachedTokens}/Audio={AudioTokens}/Reasoning={ReasoningTokens}, " +
                "Streaming: {IsStreaming}",
                openAiUsage.RequestId,
                openAiUsage.Model,
                openAiUsage.Usage.PromptTokens,
                openAiUsage.Usage.CompletionTokens,
                openAiUsage.Usage.TotalTokens,
                openAiUsage.Usage.PromptTokensDetails?.CachedTokens ?? 0,
                openAiUsage.Usage.PromptTokensDetails?.AudioTokens ?? 0,
                openAiUsage.Usage.CompletionTokensDetails?.ReasoningTokens ?? 0,
                openAiUsage.IsStreaming);

            // Store usage data (implement your storage logic here)
            await StoreOpenAiUsageData(openAiUsage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process OpenAI token usage for request {RequestId}", usageResult.RequestId);
        }
    }

    private async Task StoreOpenAiUsageData(OpenAiUsageResult usageResult)
    {
        await Task.CompletedTask;

        // Example implementation ideas:
        /*
        await _usageRepository.SaveAsync(new OpenAiUsageRecord
        {
            RequestId = usageResult.RequestId,
            Timestamp = usageResult.Timestamp,
            Model = usageResult.Model,
            PromptTokens = usageResult.Usage.PromptTokens,
            CompletionTokens = usageResult.Usage.CompletionTokens,
            TotalTokens = usageResult.Usage.TotalTokens,
            CachedTokens = usageResult.Usage.PromptTokensDetails?.CachedTokens,
            AudioTokens = usageResult.Usage.PromptTokensDetails?.AudioTokens,
            ReasoningTokens = usageResult.Usage.CompletionTokensDetails?.ReasoningTokens,
            IsStreaming = usageResult.IsStreaming
        });
        
        // Send metrics
        _metricsCollector.RecordOpenAiUsage(usageResult);
        */
    }
}