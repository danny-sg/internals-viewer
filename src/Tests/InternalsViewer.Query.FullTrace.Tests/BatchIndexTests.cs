using System.Text;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Native;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class BatchIndexTests
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

    private const ushort PureFlag = 1;

    [Fact]
    public void A_Call_Reads_Back_Its_Batch()
    {
        var batch = Assert.IsType<BatchCapture>(Build().GetBatch(Function, 0, 1));

        Assert.Equal((Address, Iterator, 100ul, 1), (batch.Address, batch.Iterator, batch.Id, batch.Call));
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
        var log = new BatchIndex.Log();

        Add(log, call: 2, id: 200);

        Add(log, call: 1, id: 100);

        var index = log.Build();

        Assert.Equal([1, 2], index.GetCalls(Function, 0));
        Assert.Equal(200ul, index.GetBatch(Function, 0, 2)?.Id);
        Assert.True(index.HasBatch(Function, 0, 1));
        Assert.False(index.HasBatch(Function, 0, 3));
    }

    [Fact]
    public void A_Summary_Is_Read_Without_The_Slots()
    {
        var summary = Assert.Single(Build().GetSummaries(Function, 0));

        Assert.Equal(new BatchSummary(1, Iterator, BatchCaptureFlags.None, 3, 6, 3, 1, 900), summary);
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

        var log = new BatchIndex.Log();

        Add(log, call: 2, id: 100);

        log.Build().AnnotateNodes(store);

        Assert.Equal((true, false), (filter.HasBatches, other.HasBatches));
    }

    [Fact]
    public void A_Next_Batch_Call_Shows_The_Batch_It_Handed_Over()
    {
        var (index, node) = BuildProducer();

        var batch = Assert.IsType<BatchCapture>(index.GetBatch(FilterNextBatch, Consumer, 0));

        Assert.Equal((200ul, FilterNextBatch, 0), (batch.Id, batch.Function, batch.Call));
        Assert.Equal(0, Assert.Single(index.GetSummaries(FilterNextBatch, Consumer)).Call);
        Assert.Equal([0], index.GetCalls(FilterNextBatch, Consumer));
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

        Assert.Equal(100ul, lineage.Input?.Id);
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
    public void A_Summary_Counts_The_Rows_Its_Input_Selected()
    {
        var summary = Assert.Single(BuildFiltered().GetSummaries(Filter, Consumer));

        Assert.Equal((1, 3), (summary.SelectedCount, summary.InputSelectedCount));
    }

    [Fact]
    public void A_Batch_Outside_Its_Chunk_Is_Skipped()
    {
        var log = new BatchIndex.Log();

        log.Add([new CapturedBatch(Function, 0, Iterator, Address, Consumer, 100, 1, 0, 4, 0, 0, 0)], [], [0, 1, 2], [], [], []);

        Assert.Equal(0, log.Build().Count);
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

        var batch = BatchIndex.Read(reader).GetBatch(Function, 0, 1)!;

        Assert.Equal([0, 2, 5], batch.Selection);
        Assert.Equal(900, batch.Capacity);
        Assert.Equal(BatchVectorFlags.Pure, batch.Vectors[1].Flags);
        Assert.Equal("REF-0003148000", Encoding.ASCII.GetString(batch.Vectors[2].GetDeepValue(2)!.Data));
    }

    private static (BatchIndex Index, CallStackNode Node) BuildProducer()
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

        calls.Add(FilterNextBatch, Consumer, Chunk(Row(1, id: 150, endId: 190), Row(2, id: 210, endId: 250)), 2);

        var store = calls.Build();

        store.MapNodes([node]);

        var index = BuildFiltered();

        index.AnnotateNodes(store);

        return (index, node);
    }

    private static BatchIndex BuildFiltered()
    {
        var log = new BatchIndex.Log();

        Add(log, call: 1, id: 100);

        log.Add([new CapturedBatch(Filter, Consumer, Consumer, Address, Helper, 200, 1, 0, 1, 0, 1, 0)],
                [new CapturedVector(0, 4, 0, 0, 1, 0)],
                [3],
                [0x600220, 0x6011C0, 0x602160, 0x603100],
                [],
                []);

        return log.Build();
    }

    private static BatchIndex Build()
    {
        var log = new BatchIndex.Log();

        Add(log, call: 1, id: 100);

        return log.Build();
    }

    private static void Add(BatchIndex.Log log, uint call, ulong id)
    {
        var text = Encoding.ASCII.GetBytes("REF-0003148000");

        log.Add([new CapturedBatch(Function, 0, Iterator, Address, Consumer, id, call, 0, 3, 0, 3, 0, 900)],
                [new CapturedVector(0, 6, 0, 0, 1, 0),
                 new CapturedVector(6, 1, 0, 0, 3, PureFlag),
                 new CapturedVector(7, 6, 0, 1, 7, 0)],
                [0, 2, 5],
                [0x600220, 0x6011C0, 0x602160, 0x603100, 0x6040A0, 0x605040, DictionarySlot, 1, 1, DeepSlot, 1, 1, 1],
                [new CapturedDeepValue(2, 0, (uint)text.Length, (uint)text.Length)],
                text);
    }
}
