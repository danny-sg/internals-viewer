using InternalsViewer.Query.FullTrace.Timeline.Tracks;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class TraceHashTableMapTests
{
    private const uint PartitionMask = 0xE0000000;

    private const int PartitionShift = 29;

    private const uint BucketMask = 0x1FF;

    [Theory]
    [InlineData(0x596F0DDFu, 2, 0x1DF)]
    [InlineData(0x7E568AB5u, 3, 181)]
    [InlineData(0xEF616B3Eu, 7, 0x13E)]
    public void A_Hash_Selects_Its_Partition_And_Bucket_With_The_Sizes_Of_The_Hash_Table(uint hash, int partition, int bucket)
        => Assert.Equal((partition, bucket), TraceHashTableMap.GetLocation(hash, PartitionMask, PartitionShift, BucketMask));
}
