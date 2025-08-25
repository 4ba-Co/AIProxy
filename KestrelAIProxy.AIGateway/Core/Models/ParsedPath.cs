namespace KestrelAIProxy.AIGateway.Core.Models;

public sealed class ParsedPath
{
    public string[] Segments { get; set; } = [];
    public string QueryString { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string ProviderName => Segments.Length > 0 ? Segments[0] : string.Empty;
    public string[] ProviderSegments => Segments.Length > 1 ? Segments[1..] : [];
}