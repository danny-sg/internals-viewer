using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Services.Indexes;

/// <summary>
/// Service to provide index structure information
/// </summary>
public sealed class IndexService(ILogger<IndexService> logger)
{
    private const int MaxParallelPageLoads = 16;

    public const int ProgressReportInterval = 4096;

    private const int CdClusterSize = 30;

    private static readonly List<PageAddress> EmptyDownPointers = [];

    private static ReadOnlySpan<byte> CdShortDataSizes => [0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 1, 0, 0, 0];

    private ILogger<IndexService> Logger { get; } = logger;

    /// <summary>
    /// Gets the index nodes for an index, starting from a root node page address
    /// </summary>
    public async Task<List<IndexNode>> GetNodes(DatabaseSource database,
                                                PageAddress rootPage,
                                                CancellationToken cancellationToken,
                                                IProgress<int>? progress = null)
    {
        var start = Stopwatch.GetTimestamp();

        var loadedPageCount = 0;

        void OnPageLoaded()
        {
            var count = Interlocked.Increment(ref loadedPageCount);

            if (count % ProgressReportInterval == 0)
            {
                progress?.Report(count);
            }
        }

        var nodes = new List<IndexNode>();

        var nodesByAddress = new Dictionary<PageAddress, IndexNode>();

        var rootNode = new IndexNode(rootPage) { Level = 0, Ordinal = 1 };

        nodes.Add(rootNode);

        nodesByAddress[rootPage] = rootNode;

        var currentLevel = new List<IndexNode> { rootNode };
        byte level = 0;

        while (currentLevel.Count > 0)
        {
            // I/O for the whole level, in parallel
            var loaded = await LoadLevel(database, currentLevel, OnPageLoaded, cancellationToken);

            // Node construction for the next level, single-threaded and in order
            var nextLevel = new List<IndexNode>();

            for (var i = 0; i < currentLevel.Count; i++)
            {
                var node = currentLevel[i];
                var page = loaded[i];

                node.PageType = page.PageType;
                node.PreviousPage = page.PreviousPage;
                node.NextPage = page.NextPage;
                node.IndexLevel = page.IndexLevel;

                foreach (var childAddress in page.DownPointers)
                {
                    if (!nodesByAddress.TryGetValue(childAddress, out var childNode))
                    {
                        childNode = new IndexNode(childAddress)
                        {
                            Level = (byte)(level + 1),
                            Ordinal = (ushort)(nextLevel.Count + 1)
                        };

                        nodes.Add(childNode);
                        
                        nodesByAddress[childAddress] = childNode;

                        nextLevel.Add(childNode);
                    }

                    if (childNode.Parent == PageAddress.Empty)
                    {
                        childNode.Parent = node.PageAddress;
                    }
                    else if (childNode.Parent != node.PageAddress)
                    {
                        Logger.LogDebug("Page {PageAddress} has multiple parents - keeping {Parent}, ignoring {Ignored}",
                                        childNode.PageAddress,
                                        childNode.Parent,
                                        node.PageAddress);
                    }
                }
            }

            currentLevel = nextLevel;

            level++;
        }

        progress?.Report(loadedPageCount);

        Logger.LogInformation("Index loaded in {Duration}", Stopwatch.GetElapsedTime(start));

        return nodes;
    }

    /// <summary>
    /// Reads every page on a level in parallel, returning results in the same order as the input
    /// </summary>
    private static async Task<LoadedPage[]> LoadLevel(DatabaseSource database,
                                                      List<IndexNode> levelNodes,
                                                      Action onPageLoaded,
                                                      CancellationToken cancellationToken)
    {
        var results = new LoadedPage[levelNodes.Count];

        await Parallel.ForEachAsync(GetReadOrder(levelNodes),
                                    new ParallelOptions
                                    {
                                        MaxDegreeOfParallelism = MaxParallelPageLoads,
                                        CancellationToken = cancellationToken
                                    },
                                    async (i, ct) =>
                                    {
                                        results[i] = await LoadPage(database,
                                                                    levelNodes[i].PageAddress,
                                                                    ct);

                                        onPageLoaded();
                                    });

        return results;
    }

    private static int[] GetReadOrder(List<IndexNode> levelNodes)
    {
        var keys = new long[levelNodes.Count];

        var indexes = new int[levelNodes.Count];

        for (var i = 0; i < levelNodes.Count; i++)
        {
            var pageAddress = levelNodes[i].PageAddress;

            keys[i] = ((long)pageAddress.FileId << 32) | (uint)pageAddress.PageId;

            indexes[i] = i;
        }

        Array.Sort(keys, indexes);

        return indexes;
    }

    /// <summary>
    /// Reads a single page and extracts the header fields and child pointers needed to build the tree
    /// </summary>
    private static async Task<LoadedPage> LoadPage(DatabaseSource database,
                                                   PageAddress pageAddress,
                                                   CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(PageData.Size);

        try
        {
            await database.Connection.PageReader.ReadInto(database.Name, pageAddress, buffer, cancellationToken);

            return ParsePage(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static LoadedPage ParsePage(byte[] data)
    {
        ReadOnlySpan<byte> page = data.AsSpan(0, PageData.Size);

        var pageType = (PageType)page[PageHeaderParser.PageTypeOffset];

        var level = page[PageHeaderParser.LevelOffset];

        var previousPage = PageAddressParser.Parse(page, PageHeaderParser.PreviousPageOffset);

        var nextPage = PageAddressParser.Parse(page, PageHeaderParser.NextPageOffset);

        // Only index pages above the leaf (Level >= 1) point down to child pages
        var downPointers = pageType == PageType.Index && level >= 1
                           ? GetDownPointers(page)
                           : EmptyDownPointers;

        return new LoadedPage(pageType, previousPage, nextPage, level, downPointers);
    }

    private static List<PageAddress> GetDownPointers(ReadOnlySpan<byte> page)
    {
        var slotCount = BinaryPrimitives.ReadUInt16LittleEndian(page[PageHeaderParser.SlotCountOffset..]);

        var fixedLengthSize = BinaryPrimitives.ReadUInt16LittleEndian(page[PageHeaderParser.FixedLengthOffset..]);

        var downPointers = new List<PageAddress>(slotCount);

        for (var slot = 0; slot < slotCount; slot++)
        {
            var slotOffset = BinaryPrimitives.ReadUInt16LittleEndian(page[(PageData.Size - ((slot + 1) * sizeof(ushort)))..]);

            if (slotOffset < PageHeader.Size || slotOffset >= PageData.Size)
            {
                continue;
            }

            var downPointer = (page[slotOffset] & 0b1) != 0
                ? GetCdDownPointer(page, slotOffset)
                : GetFixedVarDownPointer(page, slotOffset, fixedLengthSize);

            if (downPointer != PageAddress.Empty)
            {
                downPointers.Add(downPointer);
            }
        }

        return downPointers;
    }

    private static PageAddress GetFixedVarDownPointer(ReadOnlySpan<byte> page, int slotOffset, int fixedLengthSize)
    {
        var pointerOffset = slotOffset + fixedLengthSize - PageAddress.Size;

        if (pointerOffset < PageHeader.Size || pointerOffset + PageAddress.Size > PageData.Size)
        {
            return PageAddress.Empty;
        }

        return PageAddressParser.Parse(page[pointerOffset..]);
    }

    private static PageAddress GetCdDownPointer(ReadOnlySpan<byte> page, int slotOffset)
    {
        var recordType = (CompressedRecordType)((page[slotOffset] >> 2) & 7);

        if (recordType == CompressedRecordType.Forwarding)
        {
            return PageAddress.Empty;
        }

        var currentPosition = slotOffset + sizeof(byte);

        if (currentPosition + sizeof(ushort) > PageData.Size)
        {
            return PageAddress.Empty;
        }

        int columnCount;

        if ((page[currentPosition] & 0x80) != 0)
        {
            columnCount = ((page[currentPosition] ^ 0x80) << 8) | page[currentPosition + 1];

            currentPosition += sizeof(ushort);
        }
        else
        {
            columnCount = page[currentPosition];

            currentPosition += sizeof(byte);
        }

        if (columnCount == 0)
        {
            return PageAddress.Empty;
        }

        var descriptorArrayOffset = currentPosition;

        var descriptorArraySize = (columnCount + 1) / 2;

        if (descriptorArrayOffset + descriptorArraySize > PageData.Size)
        {
            return PageAddress.Empty;
        }

        var shortDataOffset = descriptorArrayOffset + descriptorArraySize + ((columnCount - 1) / CdClusterSize);

        for (var i = 0; i < columnCount; i++)
        {
            var descriptorByte = page[descriptorArrayOffset + (i / 2)];

            var descriptor = (ColumnDescriptorFlag)(i % 2 == 0 ? descriptorByte & 0xF : descriptorByte >> 4);

            if (i == columnCount - 1 && descriptor == ColumnDescriptorFlag.SixByteShort)
            {
                if (shortDataOffset + PageAddress.Size > PageData.Size)
                {
                    return PageAddress.Empty;
                }

                return PageAddressParser.Parse(page[shortDataOffset..]);
            }

            shortDataOffset += CdShortDataSizes[(byte)descriptor];
        }

        return PageAddress.Empty;
    }

    private readonly record struct LoadedPage(PageType PageType,
                                              PageAddress PreviousPage,
                                              PageAddress NextPage,
                                              byte IndexLevel,
                                              List<PageAddress> DownPointers);
}
