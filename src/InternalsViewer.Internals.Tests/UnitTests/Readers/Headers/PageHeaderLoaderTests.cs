using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Readers.Headers;

public class PageHeaderLoaderTests
{
    [Fact]
    public async Task Can_Read_Header()
    {
        var filePath = "./UnitTests/Test Data/Test Pages/";

        var reader = new FilePageReader(filePath);

        var data = await reader.Read("TestDatabase", new PageAddress(1, 9));

        var header = PageHeaderLoader.Read(data);

        Assert.Equal(new PageAddress(1, 9), header.PageAddress);
        Assert.Equal(1, header.HeaderVersion);
        Assert.Equal((PageType)13, header.PageType);
        Assert.Equal(0x0, header.TypeFlagBits);
        Assert.Equal(0, header.Level);
        Assert.Equal(0x200, header.FlagBits);
        Assert.Equal(99, header.ObjectId);
        Assert.Equal(0, header.IndexId);
        Assert.Equal(new PageAddress(0, 0), header.PreviousPage);
        Assert.Equal(new PageAddress(0, 0), header.NextPage);
        Assert.Equal(0, header.MinLen);
        Assert.Equal(1, header.SlotCount);
        Assert.Equal(6308, header.FreeCount);
        Assert.Equal(1882, header.FreeData);
        Assert.Equal(0, header.ReservedCount);
        Assert.Equal(new LogSequenceNumber(53, 31289, 3), header.Lsn);
        Assert.Equal(0, header.TransactionReservedCount);
        Assert.Equal(0, header.GhostRecordCount);
    }
}