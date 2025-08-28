using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class DefaultPathParser : IPathParser
{
    public ParsedPath ParsePath(string path, string queryString)
    {
        if (string.IsNullOrEmpty(path))
        {
            return new ParsedPath { OriginalPath = path };
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return new ParsedPath
        {
            Segments = segments,
            QueryString = queryString,
            OriginalPath = path
        };
    }
}