﻿using InternalsViewer.Internals.Compression;
namespace InternalsViewer.Connection.BackupFile.Compression;

/// <summary>
/// Output sink for the decompressor that retains the back reference window while streaming to a destination
/// </summary>
/// <remarks>
/// Matches can reference up to <see cref="CompressedBackupFormat.MaximumMatchOffset"/> bytes of previously
/// produced output, and that window spans blocks, so the decompressor cannot forget history as it goes. Only
/// the window has to be retained though - everything older is flushed to the destination stream, which keeps
/// memory bounded regardless of backup size.
/// </remarks>
internal sealed class SlidingWindowWriter(Stream destination,
                                          int maximumMatchOffset,
                                          int bufferSize = 4 * 1024 * 1024,
                                          int retain = 0) : IXpressOutput
{
    private readonly byte[] _buffer = new byte[Math.Max(bufferSize, maximumMatchOffset * 4)];

    private readonly int _retain = Math.Max(retain, maximumMatchOffset);

    private int _position;

    public long Length { get; private set; }

    /// <summary>
    /// Logical offset of the first byte still held in the buffer
    /// </summary>
    public long WindowStart => Length - _position;

    /// <summary>
    /// Output still held in the buffer, usable both as match history and as a read cache
    /// </summary>
    public ReadOnlySpan<byte> Window => _buffer.AsSpan(0, _position);

    /// <summary>
    /// The most recent output, being all a restart has to be seeded with
    /// </summary>
    /// <remarks>
    /// A match reaches back at most maximumMatchOffset bytes, so anything older cannot affect decoding and does not have to be captured.
    /// Checkpointing <see cref="Window"/> instead would hold a copy of the whole buffer per checkpoint for the life of the map.
    /// </remarks>
    public ReadOnlySpan<byte> History
    {
        get
        {
            var length = Math.Min(_position, maximumMatchOffset);

            return _buffer.AsSpan(_position - length, length);
        }
    }

    /// <summary>
    /// Restores the writer to a previously captured point so decoding can resume from there
    /// </summary>
    public void Seed(ReadOnlySpan<byte> history, long logicalLength)
    {
        history.CopyTo(_buffer);

        _position = history.Length;

        Length = logicalLength;
    }

    public void WriteLiteral(byte value)
    {
        EnsureSpace(1);

        _buffer[_position++] = value;

        Length++;
    }

    public void WriteMatch(int offset, int length)
    {
        if (offset > _position)
        {
            throw new InvalidDataException(
                $"Match offset {offset} reaches before the start of the retained window at output position {Length}.");
        }

        EnsureSpace(length);

        var source = _position - offset;

        for (var i = 0; i < length; i++)
        {
            _buffer[_position++] = _buffer[source++];
        }

        Length += length;
    }

    /// <summary>
    /// Emits a run of zeros to stand in for a block that could not be decoded
    /// </summary>
    /// <remarks>
    /// Keeps the stream aligned. Every MTF block records its own address, so losing bytes shifts everything
    /// after it and makes the whole remainder unreadable - emitting the declared length keeps the rest valid.
    /// </remarks>
    public void WriteZeros(int count)
    {
        while (count > 0)
        {
            EnsureSpace(1);

            var run = Math.Min(count, _buffer.Length - _position);

            _buffer.AsSpan(_position, run).Clear();

            _position += run;

            Length += run;

            count -= run;
        }
    }

    public void WriteRaw(ReadOnlySpan<byte> data)
    {
        EnsureSpace(data.Length);

        data.CopyTo(_buffer.AsSpan(_position));

        _position += data.Length;

        Length += data.Length;
    }

    public void Flush()
    {
        destination.Write(_buffer, 0, _position);

        _position = 0;
    }

    private void EnsureSpace(int required)
    {
        if (_position + required <= _buffer.Length)
        {
            return;
        }

        var retained = Math.Min(_position, _retain);

        destination.Write(_buffer, 0, _position - retained);

        Buffer.BlockCopy(_buffer, _position - retained, _buffer, 0, retained);

        _position = retained;
    }
}
