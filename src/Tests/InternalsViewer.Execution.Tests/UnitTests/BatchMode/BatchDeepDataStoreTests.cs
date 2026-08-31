using System.Data;
using InternalsViewer.Execution.BatchMode;
using InternalsViewer.Execution.BatchMode.Normalization;
using InternalsViewer.Execution.BatchMode.Vectors;

namespace InternalsViewer.Execution.Tests.UnitTests.BatchMode;

[Trait("Category", "Unit")]
[Trait("Area", "BatchMode")]
public class BatchDeepDataStoreTests
{
    /// <summary>
    /// An index based pseudo address starts where a real pointer never does, so the first slot has to be biased
    /// past the value a null takes
    /// </summary>
    [Fact]
    public void First_Stored_Value_Does_Not_Collide_With_Null()
    {
        var store = new BatchDeepDataStore();

        var slot = new BatchSlot(store.Store([1, 2, 3]));

        Assert.False(slot.IsNull);

        Assert.True(slot.IsDeepDataReference);

        Assert.Equal(BatchSlotValueType.DeepDataReference,
                     BatchSlotDenormalizer.GetValueType(slot, new BatchColumn { DataType = SqlDbType.BigInt }));
    }

    [Fact]
    public void Stored_Values_Read_Back_By_Their_Slot()
    {
        var store = new BatchDeepDataStore();

        byte[][] values = [[1, 2, 3], [], [9], [255, 0, 255, 0]];

        var slots = values.Select(v => store.Store(v)).ToArray();

        for (var i = 0; i < values.Length; i++)
        {
            Assert.Equal(values[i], store.Get(slots[i]).ToArray());
        }
    }

    [Fact]
    public void Every_Slot_The_Store_Mints_Is_Distinct_And_Tagged()
    {
        var store = new BatchDeepDataStore();

        var slots = Enumerable.Range(0, 16).Select(i => store.Store([(byte)i])).ToArray();

        Assert.Equal(slots.Length, slots.Distinct().Count());

        Assert.All(slots, s => Assert.True(new BatchSlot(s).IsDeepDataReference));
    }
}
