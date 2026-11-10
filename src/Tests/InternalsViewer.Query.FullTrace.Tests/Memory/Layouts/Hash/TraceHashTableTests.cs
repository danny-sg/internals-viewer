using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Hash;

[Trait("Category", "Unit")]
public class TraceHashTableTests
{
    private const ulong Group = 0x1000;

    private const ulong Instance = 0x2000;

    private const ulong Directory = 0x3000;

    private const ulong PointerPage = 0x4000;

    private const ulong BucketPage = 0x6000;

    private const ulong WorkfilePage = 0x8000;

    private const int BlockSize = 0x100;

    [Fact]
    public void The_Top_Bits_Of_The_Hash_Pick_The_Partition_And_The_Low_Bits_The_Bucket()
    {
        var partitions = Enumerable.Range(0, 4).Select(Partition).ToList();

        var table = new TraceHashTable(Group, 0, 0, 0xC000_0000, 30, partitions);

        Assert.Equal((3, 0x3E), table.GetLocation(0xEF61_6B3E));
    }

    [Fact]
    public void A_Hash_For_A_Partition_That_Was_Not_Read_Has_No_Location()
    {
        var table = new TraceHashTable(Group, 0, 0, 0xC000_0000, 30, [Partition(0)]);

        Assert.Null(table.GetLocation(0xEF61_6B3E));
    }

    [Theory]
    [InlineData(Group)]
    [InlineData(Instance)]
    [InlineData(Directory)]
    [InlineData(PointerPage)]
    [InlineData(BucketPage)]
    [InlineData(WorkfilePage)]
    public void Each_Block_Of_The_Table_Is_Part_Of_It(ulong block) => Assert.True(CreateTable().Contains(block, BlockSize));

    [Fact]
    public void A_Block_Outside_The_Table_Is_Not_Part_Of_It() => Assert.False(CreateTable().Contains(0x9000, BlockSize));

    private static TraceHashPartition Partition(int index) => new(index, 0, 0, 0x3F, 0, 0, [], []);

    private static TraceHashTable CreateTable()
    {
        var bucket = new TraceHashBucket(0, BucketPage, 1, 7, WorkfilePage + 0x60);

        var page = new TraceHashPage(PointerPage, 0, BucketPage, 0);

        var partition = new TraceHashPartition(0, Instance, 1, 0x3F, Directory, 1, [page], [bucket]);

        return new TraceHashTable(Group, 0, Instance, 0, 0, [partition]);
    }
}
