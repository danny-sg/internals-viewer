using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Helpers;
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
        Assert.Equal(0, header.FixedLengthSize);
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
        Assert.Equal(0, header.FixedLengthSize);
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
            "01 0D 00 00 00 02 00 00 00 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 01 00 63 00 00 00 A4 18 5A 07 " +
            "09 00 00 00 01 00 00 00 35 00 00 00 39 7A 00 00 " +
            "03 00 00 00 00 00 00 00 00 00 00 00 F0 43 21 84 " +
            "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
            "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
        }
    };
}