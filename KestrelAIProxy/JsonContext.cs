using System.Text.Json.Serialization;

namespace KestrelAIProxy;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(System.Collections.Immutable.ImmutableSortedSet<string>))]
internal partial class JsonContext : JsonSerializerContext
{
}