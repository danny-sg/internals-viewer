namespace InternalsViewer.Connection.BackupFile.Content;

/// <summary>
/// Read only Stream over a content source so the MTF block parser can work against either backing
/// </summary>
internal sealed class BackupContentStream(IBackupContentSource content) : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => content.Length;

    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var available = (int)Math.Min(buffer.Length, Length - Position);

        if (available <= 0)
        {
            return 0;
        }

        content.Read(Position, buffer[..available]);

        Position += available;

        return available;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        return Position;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
