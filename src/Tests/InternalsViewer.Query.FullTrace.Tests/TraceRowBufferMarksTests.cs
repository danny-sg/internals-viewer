using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Batches.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceRowBufferMarksTests
{
    private const uint Thread = 7;

    private const ulong Iterator = 0x1F4_EEAE_1820;

    private const ulong Helper = 0x1000;

    private const ulong GetRow = 0x2000;

    private const ulong DirectSelect = 0x3000;

    private const ulong Adapter = 0x1F4_EEAE_1900;

    private const ulong BatchAddress = 0x20F_0CA6_ECA0;

    private static readonly ExecutionOperatorEvent Scan = new()
    {
        Name = "Columnstore Index Scan",
        OperatorDescription = "Columnstore Index Scan",
        Instances = [Iterator]
    };

    private static readonly TraceSpan Whole = new(0, 1000, 0, 0, TraceSpanFlags.None);

    private static readonly CallNode[] Nodes =
    [
        new(-1, 0, Helper, Iterator, 2),
        new(0, 0, GetRow, Iterator, 2),
        new(1, 0, DirectSelect, 0, 2)
    ];

    [Fact]
    public void Only_The_Innermost_Of_Nested_Captures_Is_Marked()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 100),
                             Span(node: 1, start: 10, end: 90),
                             Span(node: 2, start: 20, end: 40));

        var marks = TraceRowBufferMarks.Build(timeline, Capture((Helper, Iterator, 0), (GetRow, Iterator, 0), (DirectSelect, 0, 0)));

        Assert.Equal([GetEnd(timeline, depth: 2, index: 0)], marks.GetMarks(Scan, TraceTimelineAxis.Position).ToArray());
    }

    [Fact]
    public void Each_Row_Returned_Is_Marked()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 100),
                             Span(node: 1, start: 10, end: 90),
                             Span(node: 0, start: 200, end: 300, call: 1),
                             Span(node: 1, start: 210, end: 290, call: 1));

        var captures = Capture((Helper, Iterator, 0), (GetRow, Iterator, 0), (Helper, Iterator, 1), (GetRow, Iterator, 1));

        var marks = TraceRowBufferMarks.Build(timeline, captures);

        Assert.Equal([GetEnd(timeline, depth: 1, index: 0), GetEnd(timeline, depth: 1, index: 1)],
                     marks.GetMarks(Scan, TraceTimelineAxis.Position).ToArray());
    }

    [Fact]
    public void Batches_Are_Marked_Apart_From_Row_Buffers()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 100),
                             Span(node: 1, start: 10, end: 90));

        var marks = TraceRowBufferMarks.Build(timeline, RowBufferStore.Empty, new BatchStore([Batch(call: 0)]));

        Assert.Equal([GetEnd(timeline, depth: 1, index: 0)], marks.GetBatchMarks(Scan, TraceTimelineAxis.Position).ToArray());
        Assert.True(marks.GetMarks(Scan, TraceTimelineAxis.Position).IsEmpty);
        Assert.Equal(1, marks.Count);
    }

    [Fact]
    public void An_Operator_Without_Captures_Has_No_Marks()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 100));

        var marks = TraceRowBufferMarks.Build(timeline, Capture((Helper, Iterator, 0)));

        var other = new ExecutionOperatorEvent { Name = "Top", OperatorDescription = "Top", Instances = [0x99] };

        Assert.True(marks.GetMarks(other, TraceTimelineAxis.Position).IsEmpty);
        Assert.False(marks.HasMarks(other));
    }

    [Fact]
    public void Rows_Of_An_Adapter_Belong_To_The_Operator_Handing_It_Batches()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 100),
                             Span(node: 1, start: 10, end: 90));

        var marks = TraceRowBufferMarks.Build(timeline,
                                              CaptureFor(Adapter, (GetRow, Iterator, 0)),
                                              new BatchStore([Batch(call: 0, consumer: Adapter)]));

        Assert.Equal([GetEnd(timeline, depth: 1, index: 0)], marks.GetMarks(Scan, TraceTimelineAxis.Position).ToArray());
    }

    [Fact]
    public void Rows_Of_An_Iterator_Without_An_Operator_Are_Not_Marked()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 100),
                             Span(node: 1, start: 10, end: 90));

        var marks = TraceRowBufferMarks.Build(timeline, CaptureFor(Adapter, (GetRow, Iterator, 0)));

        Assert.False(marks.HasMarks(Scan));
    }

    [Fact]
    public void The_Nearest_Mark_Is_Found_Across_Row_Buffers_And_Batches()
    {
        var timeline = Build(Span(node: 1, start: 10, end: 90),
                             Span(node: 1, start: 210, end: 290, call: 1));

        var marks = TraceRowBufferMarks.Build(timeline, Capture((GetRow, Iterator, 0)), new BatchStore([Batch(call: 1)]));

        var rowBufferEnd = marks.GetMarks(Scan, TraceTimelineAxis.Position)[0];

        var batchEnd = marks.GetBatchMarks(Scan, TraceTimelineAxis.Position)[0];

        var rowBuffer = marks.GetNearestMark(Scan, TraceTimelineAxis.Position, rowBufferEnd + (batchEnd - rowBufferEnd) / 4);

        var batch = marks.GetNearestMark(Scan, TraceTimelineAxis.Position, batchEnd - (batchEnd - rowBufferEnd) / 4);

        Assert.Equal((0, false), (rowBuffer?.Call, rowBuffer?.IsBatch));
        Assert.Equal((1, true), (batch?.Call, batch?.IsBatch));
    }

    private static TraceTimeline Build(params CallSpan[] spans)
    {
        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        var timeline = builder.Build(Nodes);

        var tree = new CallStackTree();

        var helper = tree.AddCall(tree.Root,
                                  new CallstackFrame { Module = "sqlmin", Rva = 0x10, Address = Helper, Instance = Iterator },
                                  1);

        var getRow = tree.AddCall(helper, new CallstackFrame { Module = "sqlmin", Rva = 0x20, Address = GetRow, Instance = Iterator }, 1);

        var directSelect = tree.AddCall(getRow, new CallstackFrame { Module = "sqlmin", Rva = 0x30, Address = DirectSelect }, 1);

        timeline.MapNodes([helper, getRow, directSelect]);

        timeline.SetLifetimes([new TraceOperatorLifetime(Thread, Scan, 0, [Whole], [Whole], MemoryInUseCurve.Empty)]);

        return timeline;
    }

    private static BatchCapture Batch(uint call, ulong consumer = 0)
        => new(GetRow, Iterator, (int)call, 100 + call, Iterator, BatchAddress, consumer, BatchCaptureFlags.None, [], []);

    private static RowBufferStore Capture(params (ulong Function, ulong Instance, uint Call)[] calls) => CaptureFor(Iterator, calls);

    private static RowBufferStore CaptureFor(ulong iterator, params (ulong Function, ulong Instance, uint Call)[] calls)
    {
        RowBufferColumn[] columns = [new RowBufferColumn(0, new RowBufferBinding(4, 2, 4, 0), 0, 0, 4, [1, 0, 0, 0])];

        return new RowBufferStore(calls.Select(c => new RowBuffer(c.Function,
                                                                  c.Instance,
                                                                  (int)c.Call,
                                                                  100 + c.Call,
                                                                  iterator,
                                                                  0x1F4_EEAE_2000,
                                                                  columns)));
    }

    private static double GetEnd(TraceTimeline timeline, int depth, int index)
        => timeline.Threads[0].Rows[depth].Ends(TraceTimelineAxis.Position)[index];

    private static CallSpan Span(int node, ulong start, ulong end, uint call = 0)
        => new(1, start, 1, end, start, end, node, Thread, call, 1);
}
