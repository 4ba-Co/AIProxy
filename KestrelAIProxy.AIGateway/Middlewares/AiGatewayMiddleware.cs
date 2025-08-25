using System.Diagnostics;
using System.Net;

using KestrelAIProxy.AIGateway.Core.Models;
using KestrelAIProxy.AIGateway.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Yarp.ReverseProxy.Forwarder;

namespace KestrelAIProxy.AIGateway.Middlewares;

public sealed class AiGatewayMiddleware(
    RequestDelegate next,
    IHttpForwarder forwarder,
    ILogger<AiGatewayMiddleware> logger)
{
    private readonly ForwarderRequestConfig _requestConfig = new()
    {
        ActivityTimeout = TimeSpan.FromSeconds(300),
        Version = HttpVersion.Version30,
        VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
    };

    private readonly HttpMessageInvoker _httpClient = new(new SocketsHttpHandler()
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false,
        EnableMultipleHttp2Connections = true,
        ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
        ConnectTimeout = TimeSpan.FromSeconds(5),
    });

    public async Task InvokeAsync(HttpContext context)
    {
        var parseResult = context.GetParseResult();
        if (parseResult is not { IsValid: true })
        {
            await next(context);
            return;
        }

        try
        {
            var transformer = new CustomTransformer(parseResult);

            var destinationPrefix = $"{parseResult.TargetScheme}://{parseResult.TargetHost}";

            var error = await forwarder.SendAsync(
                context,
                destinationPrefix,
                _httpClient,
                _requestConfig,
                transformer);
            if (error != ForwarderError.None)
            {
                logger.LogError("Forwarding error: {Error}", error);
                context.Response.StatusCode = 502;
                await context.Response.WriteAsync("Bad Gateway");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during request forwarding to {TargetUri}", parseResult.TargetUri);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error during request forwarding");
        }
    }
}

internal class CustomTransformer(ParseResult parseResult) : HttpTransformer
{
    public override async ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken)
    {
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);
        proxyRequest.Headers.Host = parseResult.TargetHost;
        proxyRequest.Headers.Remove("X-Forwarded-For");
        proxyRequest.Headers.Remove("X-Forwarded-Host");
        proxyRequest.Headers.Remove("X-Forwarded-Proto");
        proxyRequest.Headers.Remove("X-Real-IP");
        // remove Cf- headers
        proxyRequest.Headers.Remove("CF-Connecting-IP");
        proxyRequest.Headers.Remove("x-real-ip");
        proxyRequest.Headers.Remove("CF-Connecting-IPv6");
        proxyRequest.Headers.Remove("CF-Pseudo-IPv4");
        proxyRequest.Headers.Remove("True-Client-IP");
        proxyRequest.Headers.Remove("X-Forwarded-Proto");
        proxyRequest.Headers.Remove("Cf-Ray");
        proxyRequest.Headers.Remove("CF-IPCountry");

        foreach (var header in parseResult.AdditionalHeaders ?? [])
        {
            proxyRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        proxyRequest.RequestUri = parseResult.TargetUri;
    }
}