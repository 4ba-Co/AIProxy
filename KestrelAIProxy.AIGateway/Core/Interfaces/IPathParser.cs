using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core.Interfaces;

public interface IPathParser
{
    ParsedPath ParsePath(string path, string queryString);
}