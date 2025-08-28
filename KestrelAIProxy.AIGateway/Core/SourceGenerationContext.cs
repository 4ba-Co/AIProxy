using System.Text.Json.Serialization;
using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AnthropicResponse))]
[JsonSerializable(typeof(AnthropicStreamEvent))]
[JsonSerializable(typeof(AnthropicUsage))]
[JsonSerializable(typeof(TokenCostBreakdown))]
[JsonSerializable(typeof(OpenAiResponse))]
[JsonSerializable(typeof(OpenAiStreamChunk))]
[JsonSerializable(typeof(OpenAiUsage))]
[JsonSerializable(typeof(PromptTokensDetails))]
[JsonSerializable(typeof(CompletionTokensDetails))]
[JsonSerializable(typeof(OpenAiUsageResult))]
[JsonSerializable(typeof(AnthropicUsageResult))]
[JsonSerializable(typeof(BaseUsageResult))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}