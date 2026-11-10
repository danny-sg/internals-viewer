using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Hash;

[Trait("Category", "Unit")]
public class TraceHashTableTests
{
    [Fact]
    public void The_Top_Bits_Of_The_Hash_Pick_The_Partition_And_The_Low_Bits_The_Bucket()
    {
        var partitions = Enumerable.Range(0, 4).Select(i => new TraceHashPartition(i, 0, 0, 0x3F, [])).ToList();

        var table = new TraceHashTable(0, 0, 0xC000_0000, 30, partitions);

        Assert.Equal((3, 0x3E), table.GetLocation(0xEF61_6B3E));
    }

    [Fact]
    public void A_Hash_For_A_Partition_That_Was_Not_Read_Has_No_Location()
    {
        var table = new TraceHashTable(0, 0, 0xC000_0000, 30, [new TraceHashPartition(0, 0, 0, 0x3F, [])]);

        Assert.Null(table.GetLocation(0xEF61_6B3E));
    }
}
