using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Tests.UnitTests.Engine.Address;

public class RowIdentifierTests
{
    [Theory]
    [InlineData("1:100:5", 1, 100, 5)]
    [InlineData("(1:100:5)", 1, 100, 5)]
    [InlineData("1,100,5", 1, 100, 5)]
    [InlineData("(1,100,5)", 1, 100, 5)]
    public void Parse_All_Formats(string input, short expectedFileId, int expectedPageId, ushort expectedSlot)
    {
        var rid = RowIdentifier.Parse(input);

        Assert.Equal(expectedFileId, rid.PageAddress.FileId);
        Assert.Equal(expectedPageId, rid.PageAddress.PageId);
        Assert.Equal(expectedSlot, rid.SlotId);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("abc:xyz:0")]
    [InlineData("")]
    public void Parse_Invalid_Format_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => RowIdentifier.Parse(input));
    }

    [Fact]
    public void Parse_Null_Throws()
    {
        Assert.Throws<ArgumentException>(() => RowIdentifier.Parse(null));
    }

    [Fact]
    public void ToString_Formats_Correctly()
    {
        var rid = new RowIdentifier(new PageAddress(1, 100), 5);

        Assert.Equal("(1:100:5)", rid.ToString());
    }

    [Fact]
    public void Byte_Array_Constructor_Parses_Correctly()
    {
        // Page id (4 bytes LE) = 100, File id (2 bytes LE) = 1, Slot (2 bytes LE) = 5
        // Layout: [PageId 4 bytes][FileId 2 bytes][SlotId 2 bytes]
        var bytes = new byte[8];
        BitConverter.GetBytes(100).CopyTo(bytes, 0);  // PageId at offset 0
        BitConverter.GetBytes((short)1).CopyTo(bytes, 4);  // FileId at offset 4
        BitConverter.GetBytes((ushort)5).CopyTo(bytes, 6); // SlotId at offset 6

        var rid = new RowIdentifier(bytes);

        Assert.Equal(1, rid.PageAddress.FileId);
        Assert.Equal(100, rid.PageAddress.PageId);
        Assert.Equal((ushort)5, rid.SlotId);
    }

    [Fact]
    public void Constructor_From_FileId_PageId_Slot_Is_Correct()
    {
        var rid = new RowIdentifier(2, 500, 10);

        Assert.Equal(2, rid.PageAddress.FileId);
        Assert.Equal(500, rid.PageAddress.PageId);
        Assert.Equal((ushort)10, rid.SlotId);
    }

    [Fact]
    public void Empty_Has_Zero_Values()
    {
        Assert.Equal(PageAddress.Empty, RowIdentifier.Empty.PageAddress);
        Assert.Equal((ushort)0, RowIdentifier.Empty.SlotId);
    }

    [Fact]
    public void Size_Is_Eight_Bytes()
    {
        Assert.Equal(8, RowIdentifier.Size);
    }
}
