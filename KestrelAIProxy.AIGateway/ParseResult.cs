using System.Diagnostics.CodeAnalysis;

namespace KestrelAIProxy.AIGateway;

public sealed class ParseResult
{
    public string TargetHost { get; set; } = null!;
    public string TargetPath { get; set; } = null!;
    public string TargetScheme { get; } = "https";
    
    public Dictionary<string, string>? AdditionalHeaders { get; set; } = [];
    public Dictionary<string, object>? Metadata { get; set; } = [];
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public Uri TargetUri => new($"{TargetScheme}://{TargetHost}{TargetPath}");
}