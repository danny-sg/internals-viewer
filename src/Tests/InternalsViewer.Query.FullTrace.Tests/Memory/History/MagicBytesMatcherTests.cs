using InternalsViewer.Query.FullTrace.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.Memory.History.MemoryHistoryStores;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MagicBytesMatcherTests
{
    private const ulong Page = 0x21C_0000_1000;

    private const ulong Moved = 0x21C_0000_2000;

    private const ulong Other = 0x21C_0000_3000;

    private const ulong Bucket = 0x21C_0000_4000;

    private static readonly byte[] Header = [0xFF, 0xFF, 0xFF, 0xFF, 0x42, 0x50, 0x53, 0x50, 0, 0, 0, 0];

    private static readonly byte[] BucketHeader = [0, 0, 0, 0, 0, 0, 0, 0x42, 0x50, 0x4F, 0x50];

    [Fact]
    public void A_Block_Is_Named_By_Magic_Bytes_Written_At_Their_Offset()
    {
        var store = Build([Block(Page, 0xEE), Block(Moved, 0xEE), Block(Other, 0xEE), Block(Bucket, 0x100)],
                          [(2, Page + 0x18, Header),
                           (2, Moved + 0x20, Header),
                           (2, Other + 0x18, [0xFF, 0xFF, 0xFF, 0xFF]),
                           (2, Bucket + 0x41, BucketHeader)]);

        var matcher = new MagicBytesMatcher(store);

        Assert.Equal(["CBpSoftPartitionPageHeader", null, null, "CBpOverflowBucketPage"],
                     new[] { Page, Moved, Other, Bucket }.Select(a => matcher.GetClassName(GetBlock(store, a))));
    }
}
