using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;

using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.AspNetCore.Http;

using Serilog;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class UniversalResponseInvoker(
    string requestId,
    string provider,
    HttpContext httpContext,
    Stream originalStream,
    IResponseProcessor processor,
    Func<BaseUsageResult, Task> onUsageDetected) : IAsyncDisposable
{
    private Stream? _streamToRead;

    public async Task InvokeAsync(
        CancellationToken cancellationToken = default)
    {
        if (originalStream.Length == 0) return;
        try
        {
            var contentEncoding = httpContext.Response.Headers.ContentEncoding.FirstOrDefault();
            if ("gzip".Equals(contentEncoding, StringComparison.OrdinalIgnoreCase))
            {
                _streamToRead = new GZipStream(originalStream, CompressionMode.Decompress, leaveOpen: true);
            }
            else if ("br".Equals(contentEncoding, StringComparison.OrdinalIgnoreCase))
            {
                _streamToRead = new BrotliStream(originalStream, CompressionMode.Decompress, leaveOpen: true);
            }
            else if ("deflate".Equals(contentEncoding, StringComparison.OrdinalIgnoreCase))
            {
                _streamToRead = new DeflateStream(originalStream, CompressionMode.Decompress, leaveOpen: true);
            }
            else
            {
                _streamToRead = originalStream;
            }

            await processor.ProcessAsync(_streamToRead, requestId, DetectStreaming(httpContext), provider,
                onUsageDetected,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Response stream processing failed for {RequestId}", requestId);
        }
    }

    private static bool DetectStreaming(HttpContext httpContext)
    {
        var contentType = httpContext.Response.ContentType ?? "";

        return contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static Encoding GetEncodingFromContentType(string? contentTypeHeader)
    {
        var encoding = Encoding.UTF8; // Default to UTF-8 if not specified or invalid

        if (string.IsNullOrEmpty(contentTypeHeader))
        {
            return encoding;
        }

        try
        {
            // Use MediaTypeHeaderValue for robust parsing
            var mediaType = MediaTypeHeaderValue.Parse(contentTypeHeader);
            if (!string.IsNullOrEmpty(mediaType.CharSet))
            {
                encoding = Encoding.GetEncoding(mediaType.CharSet);
            }
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex,
                "Could not get a valid encoding from Content-Type header '{ContentType}'. Falling back to UTF-8.",
                contentTypeHeader);
            // Fallback to UTF-8
            encoding = Encoding.UTF8;
        }

        return encoding;
    }


    public async ValueTask DisposeAsync()
    {
        if (_streamToRead != null && _streamToRead != originalStream)
        {
            await _streamToRead.DisposeAsync();
        }
    }
}