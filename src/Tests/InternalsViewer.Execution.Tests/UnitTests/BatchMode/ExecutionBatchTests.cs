using System.Data;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.Tests.UnitTests.BatchMode;

[Trait("Category", "Unit")]
[Trait("Area", "BatchMode")]
public class ExecutionBatchTests
{
    private const int Capacity = 900;

    [Fact]
    public void The_Vectors_Keep_The_Same_Arrays_Across_Resets()
    {
        var batch = Create();

        var slots = batch.Vectors[0].Slots;

        var selection = batch.SelectionVector.Selection;

        batch.Reset(Capacity);

        batch.Reset(64);

        Assert.Same(slots, batch.Vectors[0].Slots);

        Assert.Same(selection, batch.SelectionVector.Selection);
    }

    [Fact]
    public void A_Partial_Reset_Narrows_The_Row_Count_But_Not_The_Capacity()
    {
        var batch = Create();

        batch.Reset(64);

        Assert.Equal(Capacity, batch.Capacity);

        Assert.Equal(64, batch.RowCount);

        Assert.Equal(64, batch.SelectionVector.RowCount);

        Assert.Equal(63, batch.SelectionVector[63]);
    }

    [Fact]
    public void A_Full_Reset_After_A_Partial_One_Selects_Every_Row_Again()
    {
        var batch = Create();

        batch.Reset(64);

        batch.Reset(Capacity);

        Assert.Equal(Capacity, batch.RowCount);

        Assert.Equal(Capacity, batch.SelectionVector.RowCount);

        Assert.Equal(Capacity - 1, batch.SelectionVector[Capacity - 1]);
    }

    [Fact]
    public void Resetting_Drops_The_Previous_Batch_Deep_Data()
    {
        var batch = Create();

        var slot = batch.DeepDataContext.Store([1, 2, 3]);

        batch.Reset(Capacity);

        Assert.Equal(slot, batch.DeepDataContext.Store([4, 5, 6]));

        Assert.Equal<byte>([4, 5, 6], batch.DeepDataContext.Get(slot).ToArray());
    }

    [Fact]
    public void Slots_Written_By_A_Longer_Batch_Are_Not_Reachable_From_A_Shorter_One()
    {
        var batch = Create();

        batch.Vectors[0].Slots[800] = BatchSlotNormalizer.FromDictionaryDataId(7);

        batch.Reset(64);

        for (var i = 0; i < batch.SelectionVector.RowCount; i++)
        {
            Assert.True(batch.SelectionVector[i] < batch.RowCount);
        }
    }

    private static ExecutionBatch Create()
    {
        var column = new BatchColumn
        {
            Name = "Id",
            DataType = SqlDbType.BigInt
        };

        return new ExecutionBatch(Capacity, [new BatchVector(column, Capacity)], new BatchDeepDataStore())
        {
            RowGroupId = 3
        };
    }
}
