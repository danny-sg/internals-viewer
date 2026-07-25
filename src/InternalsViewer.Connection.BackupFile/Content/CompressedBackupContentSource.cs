using InternalsViewer.Connection.BackupFile.Compression;
using InternalsViewer.Connection.BackupFile.Compression.Decoders;
using InternalsViewer.Connection.BackupFile.Compression.Index;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Connection.BackupFile.Content;

/// <summary>
/// Content source for a compressed backup with on-demand decoding
/// </summary>
/// <remarks>
/// </remarks>
internal sealed class CompressedBackupContentSource : IBackupContentSource
{
    private const int WindowSize = 16 * 1024 * 1024;

    private const int RetainSize = 8 * 1024 * 1024;

    private readonly FileStream _file;

    private readonly CompressedBackupIndex _index;

    private readonly XpressHuffmanBlockDecoder _decoder = new();

    private readonly SlidingWindowWriter _writer;

    private byte[] _payloadBuffer = new byte[ushort.MaxValue + 1];

    private readonly Lock _readLock = new();

    private int _nextBlock;

    public CompressedBackupContentSource(string filename, ILogger logger, CancellationToken cancellationToken)
    {
        _file = new FileStream(filename,
                               FileMode.Open,
                               FileAccess.Read,
                               FileShare.Read,
                               bufferSize: 1 << 20,
                               FileOptions.RandomAccess);

        _index = CompressedBackupIndexer.Build(_file, logger, cancellationToken);

        _writer = new SlidingWindowWriter(Stream.Null, WindowSize, RetainSize);
    }

    public long Length => _index.DecompressedLength;

    public int FailedBlockCount => _index.FailedBlockCount;

    public int BlockCount => _index.Blocks.Count;

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

    public void Dispose() => _file.Dispose();

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

        while (_writer.Length < end && _nextBlock < _index.Blocks.Count)
        {
            DecodeNextBlock();
        }

        if (offset < _writer.WindowStart)
        {
            throw new InvalidOperationException(
                $"Read at offset {offset} fell out of the decode window - the requested range exceeds the retained window.");
        }
    }

    private void RestartAt(BackupCheckpoint? checkpoint)
    {
        if (checkpoint is null)
        {
            _writer.Seed([], 0);

            _nextBlock = 0;

            return;
        }

        _writer.Seed(checkpoint.Window, checkpoint.DecompressedOffset);

        _nextBlock = checkpoint.BlockIndex;
    }

    private void DecodeNextBlock()
    {
        var block = _index.Blocks[_nextBlock];

        var isCompressed = block.BlockType == CompressedBlockType.Compressed;

        if (_payloadBuffer.Length < block.PayloadLength)
        {
            _payloadBuffer = new byte[block.PayloadLength];
        }

        _file.Position = block.PayloadOffset;

        _file.ReadExactly(_payloadBuffer, 0, block.PayloadLength);

        if (isCompressed)
        {
            var start = _writer.Length;

            var isHuffman = block.PayloadLength > CompressedBackupFormat.HuffmanTableLength
                            && CompressedBackupFormat.IsCanonicalHuffmanTable(
                                   _payloadBuffer.AsSpan(0, CompressedBackupFormat.HuffmanTableLength));

            if (isHuffman)
            {
                try
                {
                    _decoder.Decode(_payloadBuffer.AsMemory(0, block.PayloadLength), block.DecompressedLength, _writer);
                }
                catch (InvalidDataException)
                {
                }
            }

            _writer.WriteZeros((int)(block.DecompressedLength - (_writer.Length - start)));
        }
        else
        {
            _writer.WriteRaw(_payloadBuffer.AsSpan(0, block.PayloadLength));
        }

        _nextBlock++;
    }

}
