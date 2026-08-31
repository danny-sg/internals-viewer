using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Dictionaries;

namespace InternalsViewer.Internals.Tests.UnitTests.Columnstore;

[Trait("Category", "Unit")]
[Trait("Area", "Columnstore")]
public class StringPageMarkTests
{
    [Fact]
    public void Uncompressed_Page_Marks_Its_Header_Fields()
    {
        var page = new UncompressedStringPage
        {
            IsMarkEnabled = true,
            SubLobType = SubLobType.StringPage,
            Offset = 0x50,
            Size = 64
        };

        page.Mark();

        Assert.Equal(6, page.MarkItems.Count);
    }

    [Fact]
    public void Huffman_Page_Marks_Its_Header_And_Code_Lengths()
    {
        var page = new HuffmanStringPage
        {
            IsMarkEnabled = true,
            SubLobType = SubLobType.CompressedStringPage,
            Offset = 0x50,
            Size = 512
        };

        page.Mark();

        Assert.Equal(11, page.MarkItems.Count);
    }

    [Fact]
    public void A_Page_With_Marking_Off_Records_Nothing()
    {
        var page = new UncompressedStringPage { Offset = 0x50, Size = 64 };

        page.Mark();

        Assert.Empty(page.MarkItems);
    }
}
