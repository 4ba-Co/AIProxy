namespace KestrelAIProxy.AIGateway;

public interface IPathValidator
{
    bool ValidateMinimumSegments(string[] segments, int minimumCount, out string? errorMessage);
    bool ValidateSegmentPattern(string segment, string pattern, out string? errorMessage);
    bool ValidateNotEmpty(string value, string fieldName, out string? errorMessage);
}

public sealed class DefaultPathValidator : IPathValidator
{
    public bool ValidateMinimumSegments(string[] segments, int minimumCount, out string? errorMessage)
    {
        if (segments.Length < minimumCount)
        {
            errorMessage = $"Path requires at least {minimumCount} segments, but got {segments.Length}";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public bool ValidateSegmentPattern(string segment, string pattern, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            errorMessage = $"Segment cannot be empty. Expected pattern: {pattern}";
            return false;
        }

        errorMessage = null;
        return true;
    }

    public bool ValidateNotEmpty(string value, string fieldName, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"{fieldName} cannot be empty";
            return false;
        }

        errorMessage = null;
        return true;
    }
}