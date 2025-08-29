using System.Threading.Channels;

using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class UniversalResponseStream : Stream
{
    private readonly Stream _originalStream;
    private readonly IResponseProcessor _processor;
    private readonly string _requestId;
    private readonly string _provider;
    private readonly Func<BaseUsageResult, Task> _onUsageDetected;
    private readonly ILogger _logger;
    private readonly Channel<byte[]> _channel;
    private readonly Task _processingTask;
    private readonly CancellationToken _cancellationToken;
    private readonly HttpContext _httpContext;
    private bool _actuallyStreaming;
    private bool _firstWrite = true;

    public UniversalResponseStream(
        Stream originalStream,
        IResponseProcessor processor,
        string requestId,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        ILogger logger,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        _originalStream = originalStream;
        _processor = processor;
        _requestId = requestId;
        _provider = provider;
        _onUsageDetected = onUsageDetected;
        _logger = logger;
        _httpContext = httpContext;
        _cancellationToken = cancellationToken;

        _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

        _processingTask = StartProcessingAsync();
    }

    private async Task StartProcessingAsync()
    {
        try
        {
            await _channel.Reader.WaitToReadAsync(_cancellationToken);
            var streamForProcessor = new ChannelReaderStream(_channel.Reader, _cancellationToken);

            await _processor.ProcessAsync(
                streamForProcessor,
                _requestId,
                _actuallyStreaming,
                _provider,
                _onUsageDetected,
                _cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Response stream processing failed for {RequestId}", _requestId);
        }
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _originalStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _originalStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Use WriteAsync instead.");

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        // On first write, detect actual streaming based on response Content-Type
        if (_firstWrite)
        {
            _actuallyStreaming = DetectStreaming();
            _firstWrite = false;
        }

        await _originalStream.WriteAsync(buffer, cancellationToken);
        await _channel.Writer.WriteAsync(buffer.ToArray(), cancellationToken);
    }

    private bool DetectStreaming()
    {
        var contentType = _httpContext.Response.ContentType ?? "";

        return contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
    }

    public override async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _processingTask;
        await base.DisposeAsync();
    }
}

internal sealed class ChannelReaderStream(ChannelReader<byte[]> reader, CancellationToken token)
    : Stream
{
    private readonly Queue<byte> _buffer = new();
    private bool _completed;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_completed && _buffer.Count == 0)
            return 0;

        int bytesRead = 0;

        // Read from existing buffer first
        while (bytesRead < count && _buffer.Count > 0)
        {
            buffer[offset + bytesRead] = _buffer.Dequeue();
            bytesRead++;
        }

        // If we still need more data and channel is not completed
        while (bytesRead < count && !_completed)
        {
            try
            {
                if (await reader.WaitToReadAsync(token))
                {
                    while (bytesRead < count && reader.TryRead(out var data))
                    {
                        foreach (var b in data)
                        {
                            if (bytesRead < count)
                            {
                                buffer[offset + bytesRead] = b;
                                bytesRead++;
                            }
                            else
                            {
                                _buffer.Enqueue(b);
                            }
                        }
                    }
                }
                else
                {
                    _completed = true;
                }
            }
            catch (OperationCanceledException)
            {
                _completed = true;
            }
        }

        return bytesRead;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}