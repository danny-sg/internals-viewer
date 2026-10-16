using System.Text;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Batches.Enums;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class BatchStoreTests
{
    private const ulong Function = 0x7FF8_0020_0000;

    private const ulong Iterator = 0x20F_0CA2_E280;

    private const ulong Address = 0x20F_0CA6_ECA0;

    private const ulong Consumer = 0x20F_0CA2_E1A0;

    private const ulong Filter = 0x7FF8_0020_1000;

    private const ulong Helper = 0x20F_0CA2_E000;

    private const ulong FilterNextBatch = 0x7FF8_0020_2000;

    private const ulong DictionarySlot = 0xE60100;

    private const ulong DeepSlot = 0x20E_F6E5_005D;

    [Fact]
    public void A_Call_Reads_Back_Its_Batch()
    {
        var batch = Assert.IsType<BatchCapture>(Build().GetBatch(Function, 0, 1));

        Assert.Equal((Address, Iterator, At(100), 1), (batch.Address, batch.Iterator, batch.Position, batch.Call));
        Assert.Equal([0, 2, 5], batch.Selection);
        Assert.Equal((3, 6), (batch.SelectedCount, batch.RowCount));
        Assert.Equal([1, 3, 7], batch.Vectors.Select(v => v.VectorId));
    }

    [Fact]
    public void Every_Row_Up_To_The_Last_Selected_Has_A_Slot()
    {
        var vector = Build().GetBatch(Function, 0, 1)!.Vectors[0];

        Assert.False(vector.IsPure);
        Assert.Equal(6, vector.Slots.Length);
        Assert.Equal(0x602160ul, vector.GetSlot(2));
        Assert.Equal(0x6011C0ul, vector.GetSlot(1));
    }

    [Fact]
    public void A_Pure_Vector_Holds_One_Slot_For_Every_Row()
    {
        var vector = Build().GetBatch(Function, 0, 1)!.Vectors[1];

        Assert.True(vector.IsPure);
        Assert.Equal(DictionarySlot, vector.GetSlot(0));
        Assert.Equal(DictionarySlot, vector.GetSlot(5));
    }

    [Fact]
    public void A_Deep_Value_Is_Kept_With_Its_Slot()
    {
        var vector = Build().GetBatch(Function, 0, 1)!.Vectors[2];

        var deep = Assert.IsType<BatchDeepValue>(vector.GetDeepValue(2));

        Assert.Equal("REF-0003148000", Encoding.ASCII.GetString(deep.Data));
        Assert.Equal(14, deep.Length);
        Assert.Null(vector.GetDeepValue(0));
    }

    [Fact]
    public void Batches_Are_Found_By_Call_Whatever_Order_They_Arrive_In()
    {
        var index = new BatchStore([CreateBatch(call: 2, id: 200), CreateBatch(call: 1, id: 100)]);

        Assert.Equal([1, 2], index.GetBatches(Function, 0).Select(b => b.Call));
        Assert.Equal(At(200), index.GetBatch(Function, 0, 2)?.Position);
        Assert.True(index.HasBatch(Function, 0, 1));
        Assert.False(index.HasBatch(Function, 0, 3));
    }

    [Fact]
    public void Nodes_With_A_Batch_Are_Annotated()
    {
        var tree = new CallStackTree();

        var filter = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);

        var other = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);

        var calls = new TraceCallStore.Builder();

        calls.Add(Function, 0, Chunk(Row(1, node: 1), Row(2, node: 0), Row(3, node: 1)), 3);

        var store = calls.Build();

        store.MapNodes([other, filter]);

        new BatchStore([CreateBatch(call: 2, id: 100)]).AnnotateNodes(store);

        Assert.Equal((true, false), (filter.HasBatches, other.HasBatches));
    }

    [Fact]
    public void A_Next_Batch_Call_Shows_The_Batch_It_Handed_Over()
    {
        var (index, node) = BuildProducer();

        var batch = Assert.IsType<BatchCapture>(index.GetBatch(FilterNextBatch, Consumer, 0));

        Assert.Equal((At(200), FilterNextBatch, 0), (batch.Position, batch.Function, batch.Call));
        Assert.Equal([0], index.GetBatches(FilterNextBatch, Consumer).Select(b => b.Call));
        Assert.True(node.HasBatches);
    }

    [Fact]
    public void A_Next_Batch_Call_Without_A_Handoff_Has_No_Batch()
    {
        var (index, _) = BuildProducer();

        Assert.False(index.HasBatch(FilterNextBatch, Consumer, 1));
    }

    [Fact]
    public void A_Consumer_Finds_The_Iterator_That_Handed_It_Batches()
    {
        Assert.Equal(Consumer, BuildFiltered().GetProducer(Helper));
    }

    [Fact]
    public void A_Handoff_Is_Linked_To_The_Handoff_It_Was_Given()
    {
        var lineage = Assert.IsType<BatchLineage>(BuildFiltered().GetLineage(Filter, Consumer, 1));

        Assert.Equal(At(100), lineage.Input?.Position);
        Assert.Equal(6, lineage.RowCount);
        Assert.Equal([3], lineage.Capture.Selection);
    }

    [Fact]
    public void Rows_The_Filter_Dropped_Are_Removed()
    {
        var lineage = BuildFiltered().GetLineage(Filter, Consumer, 1)!;

        Assert.True(lineage.IsSelected(3));
        Assert.True(lineage.IsRemoved(0));
        Assert.True(lineage.IsRemoved(5));
        Assert.False(lineage.IsRemoved(1));
    }

    [Fact]
    public void A_Row_Past_The_Last_Selected_Is_Read_From_The_Input()
    {
        var lineage = BuildFiltered().GetLineage(Filter, Consumer, 1)!;

        Assert.Equal(0x605040ul, lineage.GetSlot(0, 5)?.Slot);
        Assert.Equal(0x603100ul, lineage.GetSlot(0, 3)?.Slot);
    }

    [Fact]
    public void A_Batch_Finds_The_Rows_Its_Input_Selected()
    {
        var index = BuildFiltered();

        var batch = index.GetBatch(Filter, Consumer, 1)!;

        Assert.Equal((1, 3), (batch.SelectedCount, index.GetInput(batch)?.SelectedCount));
    }

    [Fact]
    public void Batches_Survive_Export_And_Import()
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            Build().Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var batch = BatchStore.Read(reader).GetBatch(Function, 0, 1)!;

        Assert.Equal([0, 2, 5], batch.Selection);
        Assert.Equal(900, batch.Capacity);
        Assert.Equal(BatchVectorFlags.Pure, batch.Vectors[1].Flags);
        Assert.Equal("REF-0003148000", Encoding.ASCII.GetString(batch.Vectors[2].GetDeepValue(2)!.Data));
    }

    private static (BatchStore Index, CallStackNode Node) BuildProducer()
    {
        var tree = new CallStackTree();

        var frame = new CallstackFrame
        {
            Module = "sqlmin",
            Rva = 0x30,
            Resolved = new ResolvedCallstackFrame { ClassName = "CBpQScanFilter", MethodName = "BpGetNextBatch" }
        };

        var node = tree.AddCall(tree.Root, frame, 1);

        var calls = new TraceCallStore.Builder();

        calls.Add(FilterNextBatch, Consumer, Chunk(Row(150, endSequence: 190), Row(210, endSequence: 250)), 2);

        var store = calls.Build();

        store.MapNodes([node]);

        var index = BuildFiltered();

        index.AnnotateNodes(store);

        return (index, node);
    }

    private static BatchStore BuildFiltered()
    {
        var filtered = new BatchCapture(Filter,
                                        Consumer,
                                        1,
                                        At(200),
                                        Consumer,
                                        Address,
                                        Helper,
                                        BatchCaptureFlags.None,
                                        [3],
                                        [Vector(1, BatchVectorFlags.None, [0x600220, 0x6011C0, 0x602160, 0x603100])]);

        return new BatchStore([CreateBatch(call: 1, id: 100), filtered]);
    }

    private static BatchStore Build() => new([CreateBatch(call: 1, id: 100)]);

    private static BatchCapture CreateBatch(int call, ulong id)
    {
        var deep = new BatchDeepValue(14, Encoding.ASCII.GetBytes("REF-0003148000"));

        return new BatchCapture(Function,
                                0,
                                call,
                                At(id),
                                Iterator,
                                Address,
                                Consumer,
                                BatchCaptureFlags.None,
                                [0, 2, 5],
                                [Vector(1, BatchVectorFlags.None, [0x600220, 0x6011C0, 0x602160, 0x603100, 0x6040A0, 0x605040]),
                                 Vector(3, BatchVectorFlags.Pure, [DictionarySlot]),
                                 Vector(7, BatchVectorFlags.None, [1, 1, DeepSlot, 1, 1, 1], new() { [2] = deep })],
                                900);
    }

    private static BatchCaptureVector Vector(int vectorId,
                                             BatchVectorFlags flags,
                                             ulong[] slots,
                                             Dictionary<int, BatchDeepValue>? deepValues = null)
        => new(vectorId, flags, slots, deepValues ?? []);
}
