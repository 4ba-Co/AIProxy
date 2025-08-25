namespace KestrelAIProxy.AIGateway.Core.Interfaces;

public interface IPathBuilder
{
    string BuildPath(string[] segments, string? queryString = null);
    string BuildTargetUri(string scheme, string host, string[] segments, string? queryString = null);
}