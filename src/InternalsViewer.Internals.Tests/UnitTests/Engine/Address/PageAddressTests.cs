namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Address;

public class PageAddressTests
{
    [Fact]
    public void Empty_Has_Zero_FileId_And_PageId()
    {
        Assert.Equal(0, PageAddress.Empty.FileId);
        Assert.Equal(0, PageAddress.Empty.PageId);
    }

    [Theory]
    [InlineData(1, 1, "(1:1)")]
    [InlineData(2, 1000, "(2:1000)")]
    [InlineData(0, 0, "(0:0)")]
    [InlineData(3, 99999, "(3:99999)")]
    public void ToString_Formats_Correctly(short fileId, int pageId, string expected)
    {
        var address = new PageAddress(fileId, pageId);

        Assert.Equal(expected, address.ToString());
    }

    [Theory]
    [InlineData(1, 0)]   // page 1 is extent 0: (1-1)/8 = 0
    [InlineData(8, 0)]   // page 8 is extent 0: (8-1)/8 = 0
    [InlineData(9, 1)]   // page 9 is extent 1: (9-1)/8 = 1
    [InlineData(16, 1)]  // page 16 is extent 1: (16-1)/8 = 1
    [InlineData(17, 2)]  // page 17 is extent 2: (17-1)/8 = 2
    public void Extent_Is_Correct(int pageId, int expectedExtent)
    {
        var address = new PageAddress(1, pageId);

        Assert.Equal(expectedExtent, address.Extent);
    }

    [Fact]
    public void Struct_Equality_Is_Value_Based()
    {
        var a = new PageAddress(1, 100);
        var b = new PageAddress(1, 100);
        var c = new PageAddress(1, 200);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Different_FileIds_Are_Not_Equal()
    {
        var a = new PageAddress(1, 100);
        var b = new PageAddress(2, 100);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Size_Is_Six_Bytes()
    {
        Assert.Equal(6, PageAddress.Size);
    }
}
