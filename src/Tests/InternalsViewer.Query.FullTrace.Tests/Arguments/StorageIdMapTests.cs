using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Arguments;

[Trait("Category", "Unit")]
public class StorageIdMapTests
{
    private const long Partition = 72057594047234048;

    private const long InRow = 72057594053853184;

    private const long LargeObject = 72057594053918720;

    private const long SystemPartition = 196608;

    [Fact]
    public void A_HoBt_Id_Is_Named_After_Its_Index()
    {
        var map = Build();

        var storageId = map.GetStorageId(Partition);

        Assert.NotNull(storageId);
        Assert.Equal(StorageIdType.HoBt, storageId.Type);
        Assert.Equal("HoBT", storageId.TypeName);
        Assert.Equal("Orders.PK_Orders", storageId.Name);
    }

    [Fact]
    public void An_Allocation_Unit_Id_Is_Named_With_Its_Type()
    {
        var map = Build();

        Assert.Equal("Orders.PK_Orders (In Row Data)", map.GetStorageId(InRow)?.Name);
        Assert.Equal("Orders.PK_Orders (Large Object Data)", map.GetStorageId(LargeObject)?.Name);
        Assert.Equal("Allocation Unit", map.GetStorageId(LargeObject)?.TypeName);
    }

    [Fact]
    public void An_Allocation_Unit_Opens_The_HoBt_It_Belongs_To()
    {
        var map = Build();

        Assert.Equal((ulong)Partition, map.GetStorageId(LargeObject)?.HoBtId);
    }

    [Fact]
    public void An_Id_That_Is_Both_A_HoBt_And_Its_Allocation_Unit_Is_Named_As_The_HoBt()
    {
        var map = StorageIdMap.Build([Unit(Partition, Partition, AllocationUnitType.InRowData, "sysschobjs", "clst")]);

        Assert.Equal(StorageIdType.HoBt, map.GetStorageId(Partition)?.Type);
    }

    [Fact]
    public void A_Small_Value_Is_Not_Matched_Unless_It_Is_Passed_As_A_HoBt()
    {
        var map = StorageIdMap.Build([Unit(SystemPartition, SystemPartition, AllocationUnitType.InRowData, "sysrscols", "clst")]);

        Assert.Null(map.GetStorageId(SystemPartition));
        Assert.Equal("sysrscols.clst", map.GetHoBt(SystemPartition)?.Name);
    }

    [Fact]
    public void An_Allocation_Unit_Id_Is_Not_A_HoBt()
    {
        var map = Build();

        Assert.Null(map.GetHoBt(InRow));
        Assert.Null(map.GetStorageId(Partition + 1));
    }

    [Fact]
    public void A_Heap_Is_Named_After_Its_Table()
    {
        var map = StorageIdMap.Build([Unit(Partition, InRow, AllocationUnitType.InRowData, "Orders", string.Empty)]);

        Assert.Equal("Orders", map.GetStorageId(Partition)?.Name);
    }

    private static StorageIdMap Build()
        => StorageIdMap.Build([Unit(Partition, InRow, AllocationUnitType.InRowData, "Orders", "PK_Orders"),
                               Unit(Partition, LargeObject, AllocationUnitType.LargeObjectData, "Orders", "PK_Orders")]);

    private static AllocationUnit Unit(long partitionId, long allocationUnitId, AllocationUnitType type, string table, string index)
        => new()
        {
            PartitionId = partitionId,
            AllocationUnitId = allocationUnitId,
            AllocationUnitType = type,
            TableName = table,
            IndexName = index
        };
}
