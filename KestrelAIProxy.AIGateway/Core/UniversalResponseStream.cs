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
    private readonly bool _initialStreamingPrediction;
    private readonly string _provider;
    private readonly Func<BaseUsageResult, Task> _onUsageDetected;
    private readonly ILogger _logger;
    private readonly Channel<byte[]> _channel;
    private readonly Task _processingTask;
    private readonly CancellationToken _cancellationToken;
    private readonly HttpContext _httpContext;
    private bool? _actuallyStreaming;
    private bool _firstWrite = true;

    public UniversalResponseStream(
        Stream originalStream,
        IResponseProcessor processor,
        string requestId,
        bool isStreaming,
        string provider,
        Func<BaseUsageResult, Task> onUsageDetected,
        ILogger logger,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        _originalStream = originalStream;
        _processor = processor;
        _requestId = requestId;
        _initialStreamingPrediction = isStreaming;
        _provider = provider;
        _onUsageDetected = onUsageDetected;
        _logger = logger;
        _httpContext = httpContext;
        _cancellationToken = cancellationToken;

        _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true
        });

        _processingTask = StartProcessingAsync();
    }

    private async Task StartProcessingAsync()
    {
        try
        {
            // Wait for first write to determine actual streaming mode
            var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var combined = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, timeout.Token);

            try
            {
                await _channel.Reader.WaitToReadAsync(combined.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                // Timeout waiting for first data - use initial prediction
            }

            var streamForProcessor = new ChannelReaderStream(_channel.Reader, _cancellationToken);

            // Use actual streaming detection if available, fallback to initial prediction
            var isActuallyStreaming = _actuallyStreaming ?? _initialStreamingPrediction;

            await _processor.ProcessAsync(
                streamForProcessor,
                _requestId,
                isActuallyStreaming,
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
            _actuallyStreaming = DetectActualStreaming();
            _firstWrite = false;

            if (_actuallyStreaming != _initialStreamingPrediction)
            {
                _logger.LogTrace("Stream detection for {RequestId}: predicted={Predicted}, actual={Actual}",
                    _requestId, _initialStreamingPrediction, _actuallyStreaming);
            }
        }

        await _originalStream.WriteAsync(buffer, cancellationToken);
        await _channel.Writer.WriteAsync(buffer.ToArray(), cancellationToken);
    }

    private bool DetectActualStreaming()
    {
        var contentType = _httpContext.Response.ContentType ?? "";

        // Check for text/event-stream (Server-Sent Events)
        if (contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogTrace("Streaming detected: {ContentType}", contentType);
            return true;
        }

        // Check for application/x-ndjson or other streaming content types
        if (contentType.Contains("application/x-ndjson", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("application/stream+json", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogTrace("Streaming detected: {ContentType}", contentType);
            return true;
        }


        // Fallback to initial prediction if no clear indicators
        _logger.LogTrace("No streaming headers found, using prediction: {Prediction}",
            _initialStreamingPrediction);
        return _initialStreamingPrediction;
    }

    public override async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _processingTask;
        await base.DisposeAsync();
    }
}

internal sealed class ChannelReaderStream : Stream
{
    private readonly ChannelReader<byte[]> _reader;
    private readonly CancellationToken _cancellationToken;
    private readonly Queue<byte> _buffer = new();
    private bool _completed;

    public ChannelReaderStream(ChannelReader<byte[]> reader, CancellationToken cancellationToken)
    {
        _reader = reader;
        _cancellationToken = cancellationToken;
    }

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
                if (await _reader.WaitToReadAsync(_cancellationToken))
                {
                    while (bytesRead < count && _reader.TryRead(out var data))
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