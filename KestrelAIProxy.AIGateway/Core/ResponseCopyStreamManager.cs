namespace KestrelAIProxy.AIGateway.Core;

/// <summary>
/// RAII wrapper for managing response copy stream lifecycle.
/// Ensures proper resource cleanup and prevents premature disposal.
/// </summary>
public sealed class ResponseCopyStreamManager : IAsyncDisposable
{
    private readonly MemoryStream _copyStream;
    private bool _disposed = false;

    public ResponseCopyStreamManager()
    {
        _copyStream = new MemoryStream();
    }

    /// <summary>
    /// Gets the copy stream for reading. Stream remains valid until this manager is disposed.
    /// </summary>
    public MemoryStream CopyStream 
    { 
        get 
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _copyStream;
        } 
    }

    /// <summary>
    /// Checks if the stream is still accessible for operations
    /// </summary>
    public bool IsAccessible => !_disposed && _copyStream.CanRead && _copyStream.CanSeek;

    /// <summary>
    /// Safely resets the stream position to beginning for reading
    /// </summary>
    public void ResetPosition()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_copyStream.CanSeek)
        {
            _copyStream.Position = 0;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _copyStream.DisposeAsync();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _copyStream.Dispose();
            _disposed = true;
        }
    }
}