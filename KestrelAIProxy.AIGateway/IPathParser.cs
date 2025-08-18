namespace KestrelAIProxy.AIGateway;

public interface IPathParser
{
    ParsedPath ParsePath(string path, string queryString);
}

public sealed class DefaultPathParser : IPathParser
{
    public ParsedPath ParsePath(string path, string queryString)
    {
        if (string.IsNullOrEmpty(path))
        {
            return new ParsedPath { OriginalPath = path };
        }

        // 分割路径段，移除空段
        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        return new ParsedPath
        {
            Segments = segments,
            QueryString = queryString,
            OriginalPath = path
        };
    }
}