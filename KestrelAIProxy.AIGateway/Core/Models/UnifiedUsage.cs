using System.Text.Json.Serialization;

namespace KestrelAIProxy.AIGateway.Core.Models;

public abstract class BaseUsageResult
{
    public required string RequestId { get; set; }
    public required string Provider { get; set; }
    public required string Model { get; set; }
    public required bool IsStreaming { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
    
    [JsonPropertyName("prompt_tokens_details")]
    public PromptTokensDetails? PromptTokensDetails { get; set; }
    
    [JsonPropertyName("completion_tokens_details")]
    public CompletionTokensDetails? CompletionTokensDetails { get; set; }
}

public sealed class PromptTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int? CachedTokens { get; set; }
    
    [JsonPropertyName("audio_tokens")]
    public int? AudioTokens { get; set; }
}

public sealed class CompletionTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int? ReasoningTokens { get; set; }
    
    [JsonPropertyName("audio_tokens")]
    public int? AudioTokens { get; set; }
}

public sealed class OpenAiResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public object[]? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAiUsage? Usage { get; set; }

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }
}

public sealed class OpenAiStreamChunk
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("choices")]
    public object[]? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAiUsage? Usage { get; set; }
}

public sealed class OpenAiUsageResult : BaseUsageResult
{
    public required OpenAiUsage Usage { get; set; }
}

public sealed class AnthropicUsageResult : BaseUsageResult
{
    public required AnthropicUsage Usage { get; set; }
    public required decimal TotalCost { get; set; }
    public required TokenCostBreakdown CostBreakdown { get; set; }
}