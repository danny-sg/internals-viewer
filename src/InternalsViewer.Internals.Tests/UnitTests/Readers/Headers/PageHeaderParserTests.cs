using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Services.Pages.Parsers;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Readers.Headers;

public class PageHeaderParserTests
{
    [Fact]
    public async Task Can_Read_Header()
    {
        var filePath = "./UnitTests/Test Data/Test Pages/";

        var reader = new FilePageReader(filePath);

        var data = await reader.Read("TestDatabase", new PageAddress(1, 9));

        var header = PageHeaderParser.Parse(data);

        Assert.Equal(new PageAddress(1, 9), header.PageAddress);
        Assert.Equal(1, header.HeaderVersion);
        Assert.Equal((PageType)13, header.PageType);
        Assert.Equal(0x0, header.TypeFlagBits);
        Assert.Equal(0, header.Level);
        Assert.Equal(0x200, header.FlagBits);
        Assert.Equal(99, header.InternalObjectId);
        Assert.Equal(0, header.InternalIndexId);
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

    [Theory]
    [MemberData(nameof(TestHeaders))]
    public void Can_Read_Raw_Header(string value)
    {
        var data = value.ToByteArray();

        var header = PageHeaderParser.Parse(data);

        Assert.Equal(new PageAddress(1, 9), header.PageAddress);
        Assert.Equal(1, header.HeaderVersion);
        Assert.Equal((PageType)13, header.PageType);
        Assert.Equal(0x0, header.TypeFlagBits);
        Assert.Equal(0, header.Level);
        Assert.Equal(0x200, header.FlagBits);
        Assert.Equal(99, header.InternalObjectId);
        Assert.Equal(0, header.InternalIndexId);
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

    public static IEnumerable<object[]> TestHeaders = new List<object[]>
    {
        new object[]
        {
            "01 02 00 02 20 02 00 01 00 00 00 00 00 00 0F 00 " +
            "00 00 00 00 00 00 03 00 43 01 00 00 6D 1F 8D 00 " +
            "12 1B 00 00 01 00 00 00 34 00 00 00 B0 07 00 00 " +
            "E2 01 00 00 40 08 00 00 00 00 00 00 D5 24 70 72 " +
            "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
        }
    };
}