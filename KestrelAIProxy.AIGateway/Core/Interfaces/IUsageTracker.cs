using System.Text;

using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.AspNetCore.Http;

namespace KestrelAIProxy.AIGateway.Core.Interfaces;

public interface IUsageTracker
{
    string ProviderName { get; }
    bool ShouldTrack(HttpContext context, ParsedPath parsedPath);
    Task OnUsageDetectedAsync(BaseUsageResult usageResult);
}

public interface IResponseProcessor
{
    Task ProcessAsync(
        Stream responseStream,
        string requestId,
        bool isStreaming,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        CancellationToken cancellationToken = default);
}