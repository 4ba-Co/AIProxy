using KestrelAIProxy.AIGateway.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Middlewares;

public sealed class SseInterceptorMiddleware(RequestDelegate next, ILogger<SseInterceptorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {

        var originalBodyStream = context.Response.Body;

        await using var customStream = new SseParsingStream(originalBodyStream, ParseLine);
        context.Response.Body = customStream;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }


    private Task ParseLine(string line)
    {
        if (line.StartsWith("data:"))
        {
            var jsonData = line[5..].Trim();
            if (!string.IsNullOrEmpty(jsonData))
            {
                logger.LogInformation("Intercepted SSE data: {Data}", jsonData);

                // 示例：在这里执行真正的异步工作，而不会阻塞转发流
                // await Task.Delay(10); // 模拟异步数据库写入或 API 调用
                // await dbContext.SomeLogs.AddAsync(new Log { Data = jsonData });
                // await dbContext.SaveChangesAsync();
            }
        }

        return Task.CompletedTask;
    }
}