using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Readers.Headers;
using InternalsViewer.Tests.Internals.UnitTests.Services.Loaders;

namespace InternalsViewer.Tests.Internals.UnitTests.Readers;

public class KeyValueHeaderParserTests
{
    [Fact]
    public void Can_Parse_Header()
    {
        var values = TestHeader.HeaderValues;

        var result = KeyValueHeaderParser.TryParse(values, out var header);

        Assert.True(result);

        Assert.Equal(1, header.SlotCount);
        Assert.Equal(2, header.FreeCount);
        Assert.Equal(3, header.FreeData);
        Assert.Equal(PageType.Gam, header.PageType);
        Assert.Equal(0, header.Level);
        Assert.Equal(1, header.MinLen);

        Assert.Equal(1, header.IndexId);
        Assert.Equal(6488064, header.AllocationUnitId);

        Assert.Equal(99, header.ObjectId);
        Assert.Equal(0, header.PartitionId);

        Assert.Equal(1, header.ReservedCount);

        Assert.Equal(2, header.XactReservedCount);

        Assert.Equal(-273531820, header.TornBits);

        Assert.Equal(new PageAddress(1, 2), header.PageAddress);
        Assert.Equal(new PageAddress(0, 0), header.PreviousPage);
        Assert.Equal(new PageAddress(0, 0), header.NextPage);
        Assert.Equal(new LogSequenceNumber("(53:29745:7)"), header.Lsn);

        Assert.NotEmpty(header.FlagBits);
    }
}