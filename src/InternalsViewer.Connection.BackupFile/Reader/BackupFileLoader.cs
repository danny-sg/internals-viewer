using System.Buffers.Binary;
using InternalsViewer.Connection.BackupFile.Format;
using InternalsViewer.Connection.BackupFile.Format.Blocks;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Os;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Connection.BackupFile.Reader;

/// <summary>
/// Loads a SQL Server backup file into a list of descriptor blocks
/// </summary>
/// <remarks>
/// Backup files follow the MTF (Microsoft Tape Format) structure.
///
/// Files are composed of a series of blocks of different types. The BackupFileLoader reads and parses blocks sequentially.
/// </remarks>
internal sealed class BackupFileLoader(ILogger<BackupFileLoader> logger, string filename)
{
    private static readonly byte[] CompressedBackupSignature = [.. "MSSQLBAK"u8];

    private const int CommonHeaderLength = 52;

    private const int OffsetToFirstEventOffset = 8;

    private const int OsIdOffset = 10;

    private const int OsVersionOffset = 11;

    private const int FormatLogicalAddressOffset = 20;

    private const int ScanBufferLength = 0x400000;

    public ILogger<BackupFileLoader> Logger { get; } = logger;

    public BackupReader Reader { get; } = new(filename);

    private long _dataSetStartPosition = -1;

    private int _formatLogicalBlockSize;

    public List<DescriptorBlock> Load()
    {
        var blocks = new List<DescriptorBlock>();

        // MTF requires TAPE as the first block, anything else will be an incompatible format
        if (Reader.PeekNextBlockType() != BlockType.Tape)
        {
            ThrowNotMtf();
        }

        while (Reader.PeekNextBlockType() != BlockType.None)
        {
            var startPosition = Reader.BaseStream.Position;

            if (!Reader.TryReadBlock(out var block))
            {
                if (!TrySkipUnknownSection(startPosition))
                {
                    throw new InvalidDataException(
                        $"Unknown descriptor block type at offset {startPosition} and no further known blocks found.");
                }

                continue;
            }

            blocks.Add(block);

            switch (block)
            {
                case TapeHeaderDescriptorBlock tape:
                    _formatLogicalBlockSize = tape.FormatLogicalBlockSize;
                    break;

                case StartOfDataSetDescriptorBlock:
                    _dataSetStartPosition = startPosition;
                    break;

                case EndOfDataSetDescriptorBlock:
                    return blocks;
            }

            if (!block.Streams.Any())
            {
                var position = block.OffsetToFirstEvent - (Reader.BaseStream.Position - startPosition);

                Reader.BaseStream.Seek(position, SeekOrigin.Current);
            }
        }

        return blocks;
    }

    /// <summary>
    /// Safely skips unknown blocks to try and rejoin known blocks
    /// </summary>
    /// <remarks>
    /// The length of a block is dependent on its type/parsed values. If we can't parse the block we don't know when it ends.
    ///
    /// This process scans forward looking for the start of known block types, marked by 4-byte ASCII tags.
    /// </remarks>
    private bool TrySkipUnknownSection(long unknownStartPosition)
    {
        if (_dataSetStartPosition < 0 || _formatLogicalBlockSize <= 0)
        {
            return false;
        }

        Reader.BaseStream.Seek(unknownStartPosition, SeekOrigin.Begin);

        var unknownType = Reader.ReadFixedLengthString(4, StringType.Ansi);

        Logger.LogWarning("Skipping unknown descriptor block type '{BlockType}' at offset {Offset}",
                          unknownType,
                          unknownStartPosition);

        // Recognized block tags
        var knownTypes = BackupReader.KnownBlockTypes
                                     .Select(t => BitConverter.GetBytes((uint)t))
                                     .ToList();

        var buffer = new byte[ScanBufferLength + sizeof(uint)];

        var scanPosition = unknownStartPosition + sizeof(uint);

        var length = Reader.BaseStream.Length;

        while (scanPosition < length)
        {
            Reader.BaseStream.Seek(scanPosition, SeekOrigin.Begin);

            var read = Reader.BaseStream.Read(buffer, 0, buffer.Length);

            if (read < CommonHeaderLength)
            {
                break;
            }

            foreach (var typeBytes in knownTypes)
            {
                var searchArea = buffer.AsSpan(0, read);

                var index = searchArea.IndexOf(typeBytes);

                while (index >= 0)
                {
                    var candidatePosition = scanPosition + index;

                    if (IsValidBlockStart(candidatePosition))
                    {
                        Logger.LogWarning("Resuming at block at offset {Offset}", candidatePosition);

                        Reader.BaseStream.Seek(candidatePosition, SeekOrigin.Begin);

                        return true;
                    }

                    var next = searchArea[(index + 1)..].IndexOf(typeBytes);

                    index = next < 0 ? -1 : index + 1 + next;
                }
            }

            scanPosition += ScanBufferLength;
        }

        return false;
    }

    /// <summary>
    /// Bounds/consistency check that a candidate position is a genuine descriptor block start
    /// </summary>
    /// <remarks>
    /// A block type tag found by the scan proves nothing on its own - page and section payloads can contain the same
    /// 4 bytes by chance.
    ///
    /// A real common header must have a plausible OffsetToFirstEvent (52 is the header size, 4096 is a generous
    /// ceiling over observed values), the constant OS id/version, and a Format Logical Address that resolves to the
    /// candidate's own position in the file. Payload bytes cannot fake the address check as the required value
    /// depends on where in the file the bytes sit.
    /// </remarks>
    private bool IsValidBlockStart(long candidatePosition)
    {
        if (candidatePosition + CommonHeaderLength > Reader.BaseStream.Length)
        {
            return false;
        }

        Reader.BaseStream.Seek(candidatePosition, SeekOrigin.Begin);

        var header = Reader.ReadBytes(CommonHeaderLength);

        var offsetToFirstEvent = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(OffsetToFirstEventOffset));

        if (offsetToFirstEvent is < CommonHeaderLength or > 4096)
        {
            return false;
        }

        if (header[OsIdOffset] != (byte)OsId.WindowsNt || header[OsVersionOffset] != 1)
        {
            return false;
        }

        var formatLogicalAddress = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(FormatLogicalAddressOffset));

        return _dataSetStartPosition + (long)formatLogicalAddress * _formatLogicalBlockSize == candidatePosition;
    }

    private void ThrowNotMtf()
    {
        Reader.BaseStream.Seek(0, SeekOrigin.Begin);

        var signature = Reader.ReadBytes(CompressedBackupSignature.Length);

        if (signature.AsSpan().SequenceEqual(CompressedBackupSignature))
        {
            throw new NotSupportedException(
                "The backup is compressed. Only uncompressed backups can be read directly - restore the backup or " +
                "take a new backup without COMPRESSION.");
        }

        throw new InvalidDataException("The file is not a SQL Server backup in Microsoft Tape Format (MTF).");
    }
}
