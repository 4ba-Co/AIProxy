namespace KestrelAIProxy.AIGateway.Core;

/// <summary>
/// A stream wrapper that copies data to a destination stream as it is being read.
/// </summary>
public class CopyOnReadStream(Stream sourceStream, Stream copyDestinationStream) : Stream
{
    private readonly Stream _sourceStream = sourceStream ?? throw new ArgumentNullException(nameof(sourceStream));

    private readonly Stream _copyDestinationStream =
        copyDestinationStream ?? throw new ArgumentNullException(nameof(copyDestinationStream));

    // Proxy properties to the source stream
    public override bool CanRead => _sourceStream.CanRead;
    public override bool CanSeek => _sourceStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _sourceStream.Length;

    public override long Position
    {
        get => _sourceStream.Position;
        set => _sourceStream.Position = value;
    }

    // The core logic: read from source, write to copy, return to caller
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int bytesRead = await _sourceStream.ReadAsync(buffer, offset, count, cancellationToken);
        if (bytesRead > 0)
        {
            await _copyDestinationStream.WriteAsync(buffer, offset, bytesRead, cancellationToken);
        }

        return bytesRead;
    }

    // Modern override with Memory<T>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int bytesRead = await _sourceStream.ReadAsync(buffer, cancellationToken);
        if (bytesRead > 0)
        {
            await _copyDestinationStream.WriteAsync(buffer.Slice(0, bytesRead), cancellationToken);
        }

        return bytesRead;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = _sourceStream.Read(buffer, offset, count);
        if (bytesRead > 0)
        {
            _copyDestinationStream.Write(buffer, offset, bytesRead);
        }

        return bytesRead;
    }

    // Other methods that need to be implemented
    public override void Flush() => _sourceStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _sourceStream.Seek(offset, origin);
    public override void SetLength(long value) => _sourceStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sourceStream.Dispose();
            // Note: _copyDestinationStream lifecycle is managed by ResponseCopyStreamManager (RAII pattern)
            // and should not be disposed here as it may be needed after the response completes
        }

        base.Dispose(disposing);
    }
}