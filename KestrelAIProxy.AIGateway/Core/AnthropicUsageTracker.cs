using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
using KestrelAIProxy.AIGateway.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class AnthropicUsageTracker : IUsageTracker
{
    private readonly ILogger<AnthropicUsageTracker> _logger;
    private const string MessagesEndpoint = "v1/messages";

    public string ProviderName => "anthropic";

    public AnthropicUsageTracker(ILogger<AnthropicUsageTracker> logger)
    {
        _logger = logger;
    }

    public bool ShouldTrack(HttpContext context, ParsedPath parsedPath)
    {
        var parseResult = context.GetParseResult();

        // Only track Anthropic requests
        if (parseResult?.Metadata?.GetValueOrDefault("Provider") is not string provider ||
            !string.Equals(provider, ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check if this is a messages API call
        var path = context.Request.Path.Value ?? "";
        return path.Contains(MessagesEndpoint, StringComparison.OrdinalIgnoreCase);
    }

    public async Task OnUsageDetectedAsync(BaseUsageResult usageResult)
    {
        if (usageResult is not AnthropicUsageResult anthropicUsage)
        {
            _logger.LogWarning("Invalid usage result type for Anthropic tracker: {Type}", usageResult.GetType().Name);
            return;
        }

        try
        {
            // Log the usage with detailed billing information
            _logger.LogInformation(
                "Anthropic API Usage - Request: {RequestId}, Model: {Model}, " +
                "Tokens: I={InputTokens}/O={OutputTokens}/CC={CacheCreation}/CR={CacheRead}, " +
                "Cost: I=${InputCost:F6}/O=${OutputCost:F6}/CC=${CacheCreationCost:F6}/CR=${CacheReadCost:F6}, " +
                "Total: ${TotalCost:F6}, Streaming: {IsStreaming}",
                anthropicUsage.RequestId,
                anthropicUsage.Model,
                anthropicUsage.Usage.InputTokens,
                anthropicUsage.Usage.OutputTokens,
                anthropicUsage.Usage.CacheCreationInputTokens ?? 0,
                anthropicUsage.Usage.CacheReadInputTokens ?? 0,
                anthropicUsage.CostBreakdown.InputCost,
                anthropicUsage.CostBreakdown.OutputCost,
                anthropicUsage.CostBreakdown.CacheCreationCost,
                anthropicUsage.CostBreakdown.CacheReadCost,
                anthropicUsage.TotalCost,
                anthropicUsage.IsStreaming);

            // Store usage data (implement your storage logic here)
            await StoreAnthropicUsageData(anthropicUsage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Anthropic token usage for request {RequestId}", usageResult.RequestId);
        }
    }

    private async Task StoreAnthropicUsageData(AnthropicUsageResult usageResult)
    {
        await Task.CompletedTask;

        // Example implementation ideas:
        /*
        await _usageRepository.SaveAsync(new AnthropicUsageRecord
        {
            RequestId = usageResult.RequestId,
            Timestamp = usageResult.Timestamp,
            Model = usageResult.Model,
            InputTokens = usageResult.Usage.InputTokens,
            OutputTokens = usageResult.Usage.OutputTokens,
            CacheCreationInputTokens = usageResult.Usage.CacheCreationInputTokens,
            CacheReadInputTokens = usageResult.Usage.CacheReadInputTokens,
            TotalCost = usageResult.TotalCost,
            CostBreakdown = JsonSerializer.Serialize(usageResult.CostBreakdown),
            IsStreaming = usageResult.IsStreaming
        });
        
        // Update user quotas
        await _quotaService.DeductUsageAsync(userId, usageResult.TotalCost);
        
        // Send metrics
        _metricsCollector.RecordAnthropicUsage(usageResult);
        
        // High usage alerts
        if (usageResult.TotalCost > 1.0m) // $1.00 threshold
        {
            await _alertService.SendHighUsageAlert(usageResult);
        }
        */
    }
}