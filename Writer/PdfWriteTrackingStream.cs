using PdfBuilder.Document.Layout;

namespace PdfBuilder.Writer;

internal sealed class PdfWriteTrackingStream : Stream
{
    private readonly Stream _destination;
    private readonly long? _maximumBytes;
    private readonly long _initialPosition;

    public PdfWriteTrackingStream(Stream destination, long? maximumBytes)
    {
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));
        if (!destination.CanWrite) throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        if (maximumBytes.HasValue && maximumBytes.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), "Maximum output bytes must be positive when configured.");
        _maximumBytes = maximumBytes;
        _initialPosition = destination.CanSeek ? destination.Position : 0L;
    }

    public long BytesWritten { get; private set; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => BytesWritten;
    public override long Position { get => _initialPosition + BytesWritten; set => throw new NotSupportedException(); }
    public override void Flush() => _destination.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _destination.FlushAsync(cancellationToken);

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateWrite(count);
        _destination.Write(buffer, offset, count);
        BytesWritten += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ValidateWrite(buffer.Length);
        _destination.Write(buffer);
        BytesWritten += buffer.Length;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ValidateWrite(buffer.Length);
        await _destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        BytesWritten += buffer.Length;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Flush();
        base.Dispose(disposing);
    }

    private void ValidateWrite(int count)
    {
        long next = checked(BytesWritten + count);
        if (_maximumBytes.HasValue && next > _maximumBytes.Value)
            throw new PdfRenderLimitException(
                nameof(PdfRenderLimits.MaximumOutputBytes),
                $"The generated PDF exceeds the configured {_maximumBytes.Value} byte limit.");
    }
}
