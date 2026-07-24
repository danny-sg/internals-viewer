using System.Buffers.Binary;
using System.Collections.Concurrent;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Services.Indexes;
using Moq;

namespace InternalsViewer.Internals.Tests.UnitTests.Services.Indexes;

public class IndexServiceTests(ITestOutputHelper testOutput)
{
    private const ushort FixedLength = 13;

    private static readonly PageAddress Root = new(1, 100);
    private static readonly PageAddress Leaf1 = new(1, 200);
    private static readonly PageAddress Leaf2 = new(3, 300);

    public ITestOutputHelper TestOutput { get; } = testOutput;

    [Fact]
    public async Task Builds_Tree_From_Down_Pointers()
    {
        var pages = new Dictionary<PageAddress, byte[]>
        {
            [Root] = CreatePage(PageType.Index, 1, PageAddress.Empty, PageAddress.Empty, [Leaf1, Leaf2]),
            [Leaf1] = CreatePage(PageType.Data, 0, PageAddress.Empty, Leaf2, []),
            [Leaf2] = CreatePage(PageType.Data, 0, Leaf1, PageAddress.Empty, [])
        };

        var service = new IndexService(TestLogger.GetLogger<IndexService>(TestOutput));

        var nodes = await service.GetNodes(CreateDatabase(pages), Root, CancellationToken.None);

        Assert.Equal(3, nodes.Count);

        var root = nodes.Single(n => n.PageAddress == Root);

        Assert.Equal(PageType.Index, root.PageType);
        Assert.Equal(PageAddress.Empty, root.Parent);
        Assert.Equal(1, root.IndexLevel);

        var leaf1 = nodes.Single(n => n.PageAddress == Leaf1);
        var leaf2 = nodes.Single(n => n.PageAddress == Leaf2);

        Assert.Equal(PageType.Data, leaf1.PageType);
        Assert.Equal(Root, leaf1.Parent);
        Assert.Equal(Root, leaf2.Parent);
        Assert.Equal(Leaf2, leaf1.NextPage);
        Assert.Equal(Leaf1, leaf2.PreviousPage);
    }

    [Fact]
    public async Task Builds_Tree_From_Compressed_Down_Pointers()
    {
        var pages = new Dictionary<PageAddress, byte[]>
        {
            [Root] = CreateCdIndexPage(1, [Leaf1, Leaf2]),
            [Leaf1] = CreatePage(PageType.Data, 0, PageAddress.Empty, Leaf2, []),
            [Leaf2] = CreatePage(PageType.Data, 0, Leaf1, PageAddress.Empty, [])
        };

        var service = new IndexService(TestLogger.GetLogger<IndexService>(TestOutput));

        var nodes = await service.GetNodes(CreateDatabase(pages), Root, CancellationToken.None);

        Assert.Equal(3, nodes.Count);

        var leaf1 = nodes.Single(n => n.PageAddress == Leaf1);
        var leaf2 = nodes.Single(n => n.PageAddress == Leaf2);

        Assert.Equal(Root, leaf1.Parent);
        Assert.Equal(Root, leaf2.Parent);
    }

    [Fact]
    public async Task Ignores_Ghost_Index_Records()
    {
        var rootPage = CreatePage(PageType.Index, 1, PageAddress.Empty, PageAddress.Empty, [Leaf1, Leaf2]);

        rootPage[96 + FixedLength] = (byte)((byte)RecordType.GhostIndex << 1);

        var pages = new Dictionary<PageAddress, byte[]>
        {
            [Root] = rootPage,
            [Leaf1] = CreatePage(PageType.Data, 0, PageAddress.Empty, PageAddress.Empty, [])
        };

        var service = new IndexService(TestLogger.GetLogger<IndexService>(TestOutput));

        var nodes = await service.GetNodes(CreateDatabase(pages), Root, CancellationToken.None);

        Assert.Equal(2, nodes.Count);
        Assert.DoesNotContain(nodes, n => n.PageAddress == Leaf2);
    }

    [Fact]
    public async Task Ignores_Compressed_Ghost_Index_Records()
    {
        var rootPage = CreateCdIndexPage(1, [Leaf1, Leaf2]);

        rootPage[96 + 16] = (byte)(1 | ((byte)CompressedRecordType.GhostIndex << 2));

        var pages = new Dictionary<PageAddress, byte[]>
        {
            [Root] = rootPage,
            [Leaf1] = CreatePage(PageType.Data, 0, PageAddress.Empty, PageAddress.Empty, [])
        };

        var service = new IndexService(TestLogger.GetLogger<IndexService>(TestOutput));

        var nodes = await service.GetNodes(CreateDatabase(pages), Root, CancellationToken.None);

        Assert.Equal(2, nodes.Count);
        Assert.DoesNotContain(nodes, n => n.PageAddress == Leaf2);
    }

    [Fact]
    public async Task Ignores_Empty_Down_Pointers()
    {
        var pages = new Dictionary<PageAddress, byte[]>
        {
            [Root] = CreatePage(PageType.Index, 1, PageAddress.Empty, PageAddress.Empty, [Leaf1, PageAddress.Empty]),
            [Leaf1] = CreatePage(PageType.Data, 0, PageAddress.Empty, PageAddress.Empty, [])
        };

        var service = new IndexService(TestLogger.GetLogger<IndexService>(TestOutput));

        var nodes = await service.GetNodes(CreateDatabase(pages), Root, CancellationToken.None);

        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public async Task Reports_Final_Page_Count()
    {
        var pages = new Dictionary<PageAddress, byte[]>
        {
            [Root] = CreatePage(PageType.Index, 1, PageAddress.Empty, PageAddress.Empty, [Leaf1, Leaf2]),
            [Leaf1] = CreatePage(PageType.Data, 0, PageAddress.Empty, Leaf2, []),
            [Leaf2] = CreatePage(PageType.Data, 0, Leaf1, PageAddress.Empty, [])
        };

        var reported = new ConcurrentQueue<int>();

        var progress = new Mock<IProgress<int>>();

        progress.Setup(p => p.Report(It.IsAny<int>())).Callback((int count) => reported.Enqueue(count));

        var service = new IndexService(TestLogger.GetLogger<IndexService>(TestOutput));

        await service.GetNodes(CreateDatabase(pages), Root, CancellationToken.None, progress.Object);

        Assert.Equal(3, reported.Last());
    }

    private static DatabaseSource CreateDatabase(Dictionary<PageAddress, byte[]> pages)
    {
        var reader = new Mock<IPageReader>();

        reader.Setup(r => r.ReadInto(It.IsAny<string>(),
                                     It.IsAny<PageAddress>(),
                                     It.IsAny<byte[]>(),
                                     It.IsAny<CancellationToken>()))
              .Returns((string _, PageAddress pageAddress, byte[] buffer, CancellationToken _) =>
              {
                  pages[pageAddress].CopyTo(buffer, 0);

                  return Task.CompletedTask;
              });

        var connection = new Mock<IConnectionType>();

        connection.SetupGet(c => c.PageReader).Returns(reader.Object);

        return new DatabaseSource(connection.Object) { Name = "TestDatabase" };
    }

    private static byte[] CreatePage(PageType pageType,
                                     byte level,
                                     PageAddress previousPage,
                                     PageAddress nextPage,
                                     PageAddress[] downPointers)
    {
        var data = new byte[PageData.Size];

        data[1] = (byte)pageType;
        data[3] = level;

        WritePageAddress(data, 8, previousPage);
        WritePageAddress(data, 16, nextPage);

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(14), FixedLength);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(22), (ushort)downPointers.Length);

        var recordOffset = (ushort)96;

        for (var slot = 0; slot < downPointers.Length; slot++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(PageData.Size - ((slot + 1) * 2)), recordOffset);

            WritePageAddress(data, recordOffset + FixedLength - PageAddress.Size, downPointers[slot]);

            recordOffset += FixedLength;
        }

        return data;
    }

    private static byte[] CreateCdIndexPage(byte level, PageAddress[] downPointers)
    {
        var data = new byte[PageData.Size];

        data[1] = (byte)PageType.Index;
        data[3] = level;

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(22), (ushort)downPointers.Length);

        var recordOffset = (ushort)96;

        for (var slot = 0; slot < downPointers.Length; slot++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(PageData.Size - ((slot + 1) * 2)), recordOffset);

            data[recordOffset] = 0b00011001;

            data[recordOffset + 1] = 3;

            data[recordOffset + 2] = 5 | (3 << 4);
            data[recordOffset + 3] = 7;

            WritePageAddress(data, recordOffset + 4 + 4 + 2, downPointers[slot]);

            recordOffset += 16;
        }

        return data;
    }

    private static void WritePageAddress(byte[] data, int offset, PageAddress pageAddress)
    {
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), pageAddress.PageId);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 4), pageAddress.FileId);
    }
}
