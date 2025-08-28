using System.Text.Json.Serialization;

namespace KestrelAIProxy.AIGateway.Core.Models;

public sealed class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }
    
    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; set; }
    
    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; set; }
    
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

public sealed class AnthropicResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("role")]
    public string? Role { get; set; }
    
    [JsonPropertyName("model")]
    public string? Model { get; set; }
    
    [JsonPropertyName("content")]
    public object[]? Content { get; set; }
    
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
    
    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }
    
    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

public sealed class AnthropicStreamEvent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
    
    [JsonPropertyName("message")]
    public AnthropicResponse? Message { get; set; }
    
    [JsonPropertyName("delta")]
    public object? Delta { get; set; }
}


public sealed class TokenCostBreakdown
{
    public decimal InputCost { get; set; }
    public decimal OutputCost { get; set; }
    public decimal CacheCreationCost { get; set; }
    public decimal CacheReadCost { get; set; }
    public decimal TotalCost { get; set; }
}