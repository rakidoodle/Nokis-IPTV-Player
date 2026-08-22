namespace MyIPTV.Infrastructure.Networking;

internal sealed class SizeLimitedReadStream(Stream inner, long maximumBytes) : Stream
{
    private long _bytesRead;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = inner.Read(buffer, offset, count);
        RecordBytes(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = inner.Read(buffer);
        RecordBytes(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        int read = await inner.ReadAsync(buffer, cancellationToken);
        RecordBytes(read);
        return read;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        int read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        RecordBytes(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RecordBytes(int count)
    {
        _bytesRead += count;
        if (_bytesRead > maximumBytes)
        {
            throw new InvalidDataException("The server response is too large to process safely.");
        }
    }
}
