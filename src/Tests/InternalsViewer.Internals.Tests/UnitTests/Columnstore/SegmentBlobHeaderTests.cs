using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Tests.UnitTests.Columnstore;

public class SegmentBlobHeaderTests
{
    [Fact]
    public void Lays_Out_Bit_Pack_Blob_Sequentially()
    {
        var header = new SegmentBlobHeader
        {
            RleType = SegmentRleType.BitPack,
            BookmarkCount = 2,
            RleArrayCount = 2,
            BitpackUnitCount = 10
        };

        Assert.Equal(2, header.BookmarkEntryCount);

        Assert.Equal(48, header.BookmarkArrayOffset);

        Assert.Equal(64, header.RleArrayOffset);

        Assert.Equal(80, header.BitpackArrayOffset);

        Assert.Equal(160, header.ExpectedSize);
    }

    [Fact]
    public void Lays_Out_Variable_Length_Blob_Sequentially()
    {
        var header = new SegmentBlobHeader
        {
            RleType = SegmentRleType.VariableLengthData,
            BookmarkCount = 6,
            RleArrayCount = 3
        };

        Assert.Equal(4, header.BookmarkEntryCount);

        Assert.Equal(50, header.BookmarkArrayOffset);

        Assert.Equal(82, header.RleArrayOffset);

        Assert.Equal(106, header.VariableLengthDataOffset);
    }

    [Fact]
    public void Keeps_The_Store_Offset_When_Null_Runs_Extend_The_Rle_Array()
    {
        var plain = new SegmentBlobHeader
        {
            RleType = SegmentRleType.VariableLengthData,
            BookmarkCount = 6,
            RleArrayCount = 2
        };

        var nullable = new SegmentBlobHeader
        {
            RleType = SegmentRleType.VariableLengthData,
            BookmarkCount = 6,
            RleArrayCount = 4
        };

        Assert.Equal(plain.VariableLengthDataOffset + 16, nullable.VariableLengthDataOffset);

        Assert.Equal(plain.RleArrayOffset, nullable.RleArrayOffset);
    }
}
