using System.Text.Json.Serialization;

using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(System.Collections.Immutable.ImmutableSortedSet<string>))]
[JsonSerializable(typeof(AnthropicResponse))]
[JsonSerializable(typeof(AnthropicStreamEvent))]
[JsonSerializable(typeof(AnthropicUsage))]
[JsonSerializable(typeof(TokenCostBreakdown))]
internal partial class JsonContext : JsonSerializerContext
{
}