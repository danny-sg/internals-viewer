using System.Buffers.Binary;
using InternalsViewer.Connection.BackupFile.Format.Blocks;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;
using InternalsViewer.Connection.BackupFile.Format.Streams;
using InternalsViewer.Internals.Engine.Pages;
using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Connection.BackupFile.Index;

/// <summary>
/// Builds a page index map for a SQL Server backup file
/// </summary>
/// <remarks>
/// The page index allows a mapping between page address and offset in the backup file.
///
/// The index is RLE encoded, so the mappings are stored as runs of consecutive pages, storing a range of addresses/offsets.
///
/// Descriptor Blocks are iterated until the end or a EndOfDataSetDescriptorBlock is found. Only MSDA (data file stream) blocks are
/// processed, with a stream type of SqlDataStream (MQDA).
///
/// Descriptor Blocks
///     --> MSDA Blocks
///         --> SqlDataStream Streams
///
/// Streams are validated to check they are compatible with this indexer - they must be uncompressed and unencrypted. The payload is a 2
/// byte prefix followed by a whole number of 8kb pages, so alignment is checked excluding the prefix.
///
/// The stream is scanned in chunks of 128 pages, reading each page header at 8kb intervals. Pages are checked for certain backup specific
/// values:
///
///  - Header Version != 1 - Empty/zero page - no identity in the header, so it joins the current run positionally as
///    previous + 1, or is ignored if there is no current run
/// 
///  - Page Type 101 - Filler Page - padding added by BACKUP, not file content - ignored and the current run is closed
///
/// - Else the File Id and Page Id are read from the fixed header locations and added to the index
///
/// A run cannot span streams as the byte offsets are not contiguous across stream boundaries, so the current run is closed at the end of
/// each stream.
///
/// Data blocks from all stripes are scanned in Format Logical Address order as a proxy for write order, so the end-of-backup system page
/// re-dump overrides the originals regardless of which stripe it landed on.
/// </remarks>
internal static class BackupPageIndexer
{
    private const int PayloadPrefixLength = 2;

    private const int ScanBufferPages = 128;

    private const byte ExpectedHeaderVersion = 1;

    private const byte FillerPageType = 101;

    private const int HeaderVersionOffset = 0;

    private const int PageTypeOffset = 1;

    private const int PageIdOffset = 32;

    private const int FileIdOffset = 36;

    /// <summary>
    /// Builds the mappings between stripes (files), pages, and offsets in the files
    /// </summary>
    public static BackupPageLocator Build(IReadOnlyList<BackupStripe> stripes, CancellationToken cancellationToken)
    {
        var builder = new BackupPageIndexBuilder();

        var dataBlocks = new List<(BackupStripe Stripe, DescriptorBlock Block)>();

        foreach (var stripe in stripes)
        {
            foreach (var block in stripe.Blocks)
            {
                if (block is EndOfDataSetDescriptorBlock)
                {
                    break;
                }

                // Data stream blocks
                if (block.BlockType == BlockType.MSDA)
                {
                    dataBlocks.Add((stripe, block));
                }
            }
        }

        var orderedDataBlocks = dataBlocks.OrderBy(b => b.Block.FormatLogicalAddress)
                                          .ThenBy(b => b.Stripe.Index);

        // Scanned in order. Pages can appear more than once (certain allocation pages are repeated at the end to ensure a final version).
        // The ordering ensures the final version overwrites earlier versions.
        foreach (var (stripe, block) in orderedDataBlocks)
        {
            foreach (var stream in block.Streams)
            {
                if (stream.Header.StreamId != StreamTypes.SqlDataStream || stream.Header.StreamLength == 0)
                {
                    continue;
                }

                Validate(stream.Header);

                ScanStream(stripe, stream, builder, cancellationToken);

                builder.CloseRun();
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Check the stream is valid/compatible
    /// </summary>
    private static void Validate(StreamHeader header)
    {
        if (header.DataCompressionAlgorithm != 0)
        {
            throw new NotSupportedException(
                "The backup is compressed. Only uncompressed backups can be read directly - restore the backup " +
                "or take a new backup without COMPRESSION.");
        }

        if (header.DataEncryptionAlgorithm != 0)
        {
            throw new NotSupportedException(
                "The backup is encrypted. Only unencrypted backups can be read directly.");
        }

        if ((header.StreamLength - PayloadPrefixLength) % PageData.Size != 0)
        {
            throw new InvalidDataException(
                $"Unexpected data stream length {header.StreamLength} - the stream is not page aligned.");
        }
    }

    /// <summary>
    /// Scan a file sending to the builder to identify contiguous sequences of pages
    /// </summary>
    /// <remarks>
    /// Reads in chunks locating page addresses directly in the page headers.
    /// </remarks>
    private static void ScanStream(BackupStripe stripe,
                                   DataStream stream,
                                   BackupPageIndexBuilder builder,
                                   CancellationToken cancellationToken)
    {
        var payloadStart = stream.DataPosition + PayloadPrefixLength;

        var pageCount = (long)(stream.Header.StreamLength - PayloadPrefixLength) / PageData.Size;

        var buffer = new byte[ScanBufferPages * PageData.Size];

        var pageIndex = 0L;

        while (pageIndex < pageCount)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pagesToRead = (int)Math.Min(ScanBufferPages, pageCount - pageIndex);

            var chunkOffset = payloadStart + pageIndex * PageData.Size;

            ReadExactly(stripe.Handle, chunkOffset, buffer.AsSpan(0, pagesToRead * PageData.Size));

            for (var i = 0; i < pagesToRead; i++)
            {
                var page = buffer.AsSpan(i * PageData.Size, PageData.Size);

                var pageOffset = chunkOffset + (long)i * PageData.Size;

                if (page[HeaderVersionOffset] != ExpectedHeaderVersion)
                {
                    builder.TryAddUnidentifiedPage(stripe.Index, pageOffset);

                    continue;
                }

                if (page[PageTypeOffset] == FillerPageType)
                {
                    builder.CloseRun();

                    continue;
                }

                var pageId = BinaryPrimitives.ReadInt32LittleEndian(page[PageIdOffset..]);

                var fileId = BinaryPrimitives.ReadInt16LittleEndian(page[FileIdOffset..]);

                if (fileId <= 0 || pageId < 0)
                {
                    throw new InvalidDataException(
                        $"Unexpected page image at backup offset {pageOffset} - page address {fileId}:{pageId}.");
                }

                builder.AddPage(fileId, pageId, stripe.Index, pageOffset);
            }

            pageIndex += pagesToRead;
        }
    }

    private static void ReadExactly(SafeFileHandle handle, long offset, Span<byte> buffer)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = RandomAccess.Read(handle, buffer[totalRead..], offset + totalRead);

            if (read == 0)
            {
                throw new EndOfStreamException($"Unexpected end of backup file reading at offset {offset + totalRead}.");
            }

            totalRead += read;
        }
    }
}
