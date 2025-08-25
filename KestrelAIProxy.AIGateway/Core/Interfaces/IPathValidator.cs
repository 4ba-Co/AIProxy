namespace KestrelAIProxy.AIGateway.Core.Interfaces;

public interface IPathValidator
{
    bool ValidateMinimumSegments(string[] segments, int minimumCount, out string? errorMessage);
    bool ValidateSegmentPattern(string segment, string pattern, out string? errorMessage);
    bool ValidateNotEmpty(string value, string fieldName, out string? errorMessage);
}