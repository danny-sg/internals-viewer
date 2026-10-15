using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceRowBufferMarksTests
{
    private const uint Thread = 7;

    private const ulong Iterator = 0x1F4_EEAE_1820;

    private const ulong Helper = 0x1000;

    private const ulong GetRow = 0x2000;

    private const ulong DirectSelect = 0x3000;

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

        Assert.Equal([GetEnd(timeline, depth: 2, index: 0)], marks.GetMarks(Thread, TraceTimelineAxis.Position).ToArray());
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
                     marks.GetMarks(Thread, TraceTimelineAxis.Position).ToArray());
    }

    [Fact]
    public void Batches_Are_Marked_Apart_From_Row_Buffers()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 100),
                             Span(node: 1, start: 10, end: 90));

        var batches = new BatchIndex.Log();

        batches.Add([new RawBatch(GetRow, Iterator, Iterator, 0x20F_0CA6_ECA0, 0, 100, 0, 0, 0, 0, 0, 0)], [], [], [], [], []);

        var marks = TraceRowBufferMarks.Build(timeline, RowBufferIndex.Empty, batches.Build());

        Assert.Equal([GetEnd(timeline, depth: 1, index: 0)], marks.GetBatchMarks(Thread, TraceTimelineAxis.Position).ToArray());
        Assert.True(marks.GetMarks(Thread, TraceTimelineAxis.Position).IsEmpty);
        Assert.Equal(1, marks.Count);
    }

    [Fact]
    public void A_Thread_Without_Captures_Has_No_Marks()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 100));

        var marks = TraceRowBufferMarks.Build(timeline, Capture((Helper, Iterator, 0)));

        Assert.True(marks.GetMarks(Thread + 1, TraceTimelineAxis.Position).IsEmpty);
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

        return timeline;
    }

    private static RowBufferIndex Capture(params (ulong Function, ulong Instance, uint Call)[] calls)
    {
        var log = new RowBufferIndex.Log();

        foreach (var (function, instance, call) in calls)
        {
            log.Add([new RawRow(function, instance, Iterator, 0x1F4_EEAE_2000, 100 + call, call, 0, 1)],
                    [new RawColumn(0, 4, 4, 4, 2, 4, 0, 0)],
                    [1, 0, 0, 0]);
        }

        return log.Build();
    }

    private static double GetEnd(TraceTimeline timeline, int depth, int index)
        => timeline.Threads[0].Rows[depth].Ends(TraceTimelineAxis.Position)[index];

    private static CallSpan Span(int node, ulong start, ulong end, uint call = 0)
        => new(1, start, 1, end, start, end, node, Thread, call, 1);
}
