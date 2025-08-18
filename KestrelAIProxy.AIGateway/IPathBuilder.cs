namespace KestrelAIProxy.AIGateway;

public interface IPathBuilder
{
    string BuildPath(string[] segments, string? queryString = null);
    string BuildTargetUri(string scheme, string host, string[] segments, string? queryString = null);
}

public sealed class DefaultPathBuilder : IPathBuilder
{
    public string BuildPath(string[] segments, string? queryString = null)
    {
        var path = "/" + string.Join("/", segments ?? []);
        if (!string.IsNullOrEmpty(queryString))
        {
            path += "?" + (queryString.StartsWith('?') ? queryString[1..] : queryString);
        }
        return path;
    }

    public string BuildTargetUri(string scheme, string host, string[] segments, string? queryString = null)
    {
        var path = BuildPath(segments, queryString);
        return $"{scheme}://{host}{path}";
    }
}