using System.Text;
using System.Threading.Channels;

using Serilog;

namespace KestrelAIProxy.AIGateway.Core;

public sealed class SseParsingStream : Stream
{
    private readonly Stream _originalStream;
    private readonly Func<string, Task> _lineParser;
    private readonly Channel<byte[]> _channel;
    private readonly Task _processingTask;

    public SseParsingStream(Stream originalStream, Func<string, Task> lineParser,
        CancellationToken cancellationToken = default)
    {
        _originalStream = originalStream;
        _lineParser = lineParser;
        _channel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
        _processingTask = ProcessChannelAsync(cancellationToken);
    }

    private async Task ProcessChannelAsync(CancellationToken cancellationToken = default)
    {
        var lineBuffer = new StringBuilder();
        try
        {
            await foreach (var data in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                var text = Encoding.UTF8.GetString(data);
                lineBuffer.Append(text);

                int newlineIndex;
                while ((newlineIndex = lineBuffer.ToString().IndexOf('\n')) != -1)
                {
                    var line = lineBuffer.ToString(0, newlineIndex).TrimEnd('\r');
                    lineBuffer.Remove(0, newlineIndex + 1);

                    if (!string.IsNullOrEmpty(line))
                    {
                        await _lineParser(line);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error("[SseParsingStream] Unhandled exception in processing task: {Exception}", ex);
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

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Use WriteAsync instead.");

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        await _originalStream.WriteAsync(buffer, cancellationToken);
        await _channel.Writer.WriteAsync(buffer.ToArray(), cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _processingTask;
        await base.DisposeAsync();
    }
}