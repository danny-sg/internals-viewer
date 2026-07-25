using InternalsViewer.Connection.BackupFile.Compression;
using InternalsViewer.Connection.BackupFile.Compression.Chunks;
using InternalsViewer.Connection.BackupFile.Compression.Decoders;
using InternalsViewer.Connection.BackupFile.Compression.Mapping;
using InternalsViewer.Connection.BackupFile.Interfaces;
using InternalsViewer.Connection.BackupFile.Interfaces.Compression;
using Microsoft.Extensions.Logging;
using InternalsViewer.Internals.Engine.Loading;

namespace InternalsViewer.Connection.BackupFile.Content;

/// <summary>
/// Content source for a compressed backup with on-demand decoding
/// </summary>
/// <remarks>
/// </remarks>
internal sealed class CompressedContentSource : IContentSource
{
    private const int WindowSize = 16 * 1024 * 1024;

    private const int RetainSize = 8 * 1024 * 1024;

    private readonly FileStream _file;

    private readonly ChunkMap _index;

    private readonly IChunkDecoder _decoder;

    private readonly SlidingWindowWriter _writer;

    private byte[] _payloadBuffer = new byte[ushort.MaxValue + 1];

    private readonly Lock _readLock = new();

    private int _nextChunk;

    public CompressedContentSource(string filename,
                                   ILogger logger,
                                   CancellationToken cancellationToken,
                                   IProgress<ProgressDetail>? progress = null)
    {
        _file = new FileStream(filename,
                               FileMode.Open,
                               FileAccess.Read,
                               FileShare.Read,
                               bufferSize: 1 << 20,
                               FileOptions.RandomAccess);

        _decoder = ChunkDecoderFactory.Create(_file);

        _index = ChunkMapper.Build(_file,
                                   logger,
                                   cancellationToken,
                                   progress,
                                   $"Decompressing {Path.GetFileName(filename)}");

        _writer = new SlidingWindowWriter(Stream.Null, _decoder.MaximumMatchOffset, WindowSize, RetainSize);
    }

    public long Length => _index.DecompressedLength;

    public int FailedChunkCount => _index.FailedChunkCount;

    public int ChunkCount => _index.Chunks.Count;

    public void Read(long offset, Span<byte> buffer)
    {
        if (offset < 0 || offset + buffer.Length > Length)
        {
            throw new EndOfStreamException(
                $"Read of {buffer.Length} bytes at offset {offset} is outside the backup stream ({Length} bytes).");
        }

        lock (_readLock)
        {
            EnsureDecoded(offset, buffer.Length);

            var start = (int)(offset - _writer.WindowStart);

            _writer.Window.Slice(start, buffer.Length).CopyTo(buffer);
        }
    }

    public void Dispose()
    {
        _decoder.Dispose();

        _file.Dispose();
    }

    private void EnsureDecoded(long offset, int length)
    {
        var end = offset + length;

        if (offset >= _writer.WindowStart && end <= _writer.Length)
        {
            return;
        }

        var checkpoint = _index.FindCheckpoint(offset);

        var restart = offset < _writer.WindowStart
                      || (checkpoint is not null && checkpoint.DecompressedOffset > _writer.Length);

        if (restart)
        {
            RestartAt(checkpoint);
        }

        while (_writer.Length < end && _nextChunk < _index.Chunks.Count)
        {
            DecodeNextChunk();
        }

        if (offset < _writer.WindowStart)
        {
            throw new InvalidOperationException(
                $"Read at offset {offset} fell out of the decode window - the requested range exceeds the retained window.");
        }
    }

    private void RestartAt(Checkpoint? checkpoint)
    {
        if (checkpoint is null)
        {
            _writer.Seed([], 0);

            _nextChunk = 0;

            return;
        }

        _writer.Seed(checkpoint.History, checkpoint.DecompressedOffset);

        _nextChunk = checkpoint.ChunkIndex;
    }

    private void DecodeNextChunk()
    {
        var chunk = _index.Chunks[_nextChunk];

        var isCompressed = chunk.Type == ChunkType.Compressed;

        if (_payloadBuffer.Length < chunk.PayloadLength)
        {
            _payloadBuffer = new byte[chunk.PayloadLength];
        }

        _file.Position = chunk.PayloadOffset;

        _file.ReadExactly(_payloadBuffer, 0, chunk.PayloadLength);

        if (isCompressed)
        {
            var start = _writer.Length;

            if (_decoder.CanDecode(_payloadBuffer.AsSpan(0, chunk.PayloadLength)))
            {
                try
                {
                    _decoder.Decode(_payloadBuffer.AsMemory(0, chunk.PayloadLength), chunk.DecompressedLength, _writer);
                }
                catch (InvalidDataException)
                {
                }
            }

            _writer.WriteZeros((int)(chunk.DecompressedLength - (_writer.Length - start)));
        }
        else
        {
            _writer.WriteRaw(_payloadBuffer.AsSpan(0, chunk.PayloadLength));
        }

        _nextChunk++;
    }

}
