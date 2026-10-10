using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.Memory;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class TimeTravelTimelineTests
{
    private const TimeTravelTimelineAxis Instructions = TimeTravelTimelineAxis.Instructions;

    private static readonly TimeTravelCallNode[] Nodes =
    [
        new(-1, 0, 0x1000, 0, 1),
        new(0, 0, 0x2000, 0, 2),
        new(1, 0, 0x3000, 0, 1)
    ];

    [Fact]
    public void Calls_Are_Laid_Out_In_Rows_By_Call_Depth()
    {
        var timeline = Build(Span(node: 2, start: 10, end: 20),
                             Span(node: 1, start: 5, end: 30),
                             Span(node: 0, start: 0, end: 40));

        var thread = Assert.Single(timeline.Threads);

        Assert.Equal([0, 1, 2], thread.Rows.Select(r => r.NodeAt(0)));
        Assert.Equal(3, thread.Calls);
    }

    [Fact]
    public void Calls_In_A_Row_Are_Ordered_By_Where_They_Start()
    {
        var timeline = Build(Span(node: 1, start: 50, end: 60, sequence: 9),
                             Span(node: 1, start: 10, end: 20, sequence: 3));

        var row = Assert.Single(timeline.Threads).Rows[1];

        Assert.Equal([10d, 50d], row.Starts(Instructions).ToArray());
    }

    [Fact]
    public void A_Call_Is_Found_By_Where_It_Ran()
    {
        var row = Build(Span(node: 0, start: 0, end: 10), Span(node: 0, start: 20, end: 30, sequence: 2)).Threads[0].Rows[0];

        Assert.Equal(1, row.IndexAt(Instructions, 25, 0));
        Assert.Equal(-1, row.IndexAt(Instructions, 15, 0));
        Assert.Equal(1, row.IndexAt(Instructions, 19.5, 1));
    }

    [Fact]
    public void Each_Thread_Has_Its_Own_Lane_In_The_Order_It_Started()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 10, thread: 9, sequence: 5),
                             Span(node: 0, start: 0, end: 10, thread: 4, sequence: 2));

        Assert.Equal([4u, 9u], timeline.Threads.Select(t => t.ThreadId));
        Assert.Equal(10, timeline.EndOf(Instructions));
    }

    [Fact]
    public void A_Call_Without_A_Logged_Call_Reads_As_No_Call()
    {
        var span = Build(Span(node: 0, start: 0, end: 10, call: uint.MaxValue, flags: 2)).Threads[0].Rows[0].Span(Instructions, 0);

        Assert.Equal(-1, span.Call);
        Assert.Equal(TimeTravelSpanFlags.StartUnknown, span.Flags);
    }

    [Fact]
    public void A_Filter_Drops_Calls_But_Keeps_Each_Lane_And_Row()
    {
        var timeline = Build(Span(node: 2, start: 10, end: 20),
                             Span(node: 1, start: 5, end: 30),
                             Span(node: 0, start: 0, end: 40));

        var thread = Assert.Single(timeline.Where(n => n != 2).Threads);

        Assert.Equal([1, 1, 0], thread.Rows.Select(r => r.Count));
        Assert.Equal(2, thread.Depth);
        Assert.Equal(2, thread.Calls);
    }

    [Fact]
    public void Calls_Under_An_Excluded_Frame_Go_With_It()
    {
        var timeline = Build(Span(node: 2, start: 10, end: 20),
                             Span(node: 1, start: 5, end: 30),
                             Span(node: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var publish = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);
        var inner = tree.AddCall(publish, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([root, publish, inner]);

        var thread = Assert.Single(timeline.WithoutCallsUnder(n => ReferenceEquals(n, publish)).Threads);

        Assert.Equal([1, 0, 0], thread.Rows.Select(r => r.Count));
        Assert.Equal(1, thread.Calls);
    }

    [Fact]
    public void Each_Call_Path_Knows_The_Path_It_Was_Called_From()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 10));

        Assert.Equal(3, timeline.NodeCount);
        Assert.Equal([-1, 0, 1], Enumerable.Range(0, 3).Select(timeline.ParentOf));
    }

    [Fact]
    public void A_Root_Keeps_Only_Its_Calls_And_Their_Callees_With_Itself_At_The_Top()
    {
        var timeline = Build(Span(node: 2, start: 10, end: 20),
                             Span(node: 1, start: 5, end: 30),
                             Span(node: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var outer = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x10 }, 1);
        var execute = tree.AddCall(outer, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var inner = tree.AddCall(execute, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([outer, execute, inner]);

        var rooted = timeline.RootedAt(n => ReferenceEquals(n, execute));

        Assert.NotNull(rooted);

        var thread = Assert.Single(rooted.Threads);

        Assert.Equal([1, 2], thread.Rows.Select(r => r.NodeAt(0)));
        Assert.Equal(2, thread.Calls);
    }

    [Fact]
    public void Other_Threads_Show_What_They_Ran_While_The_Root_Was_Running()
    {
        TimeTravelCallNode[] nodes =
        [
            new(-1, 0, 0x1000, 0, 1),
            new(0, 0, 0x2000, 0, 1),
            new(-1, 0, 0x3000, 0, 2),
            new(2, 0, 0x4000, 0, 1)
        ];

        var builder = new TimeTravelTimeline.Builder();

        builder.Add([Span(node: 1, start: 10, end: 50),
                     Span(node: 0, start: 0, end: 60),
                     Span(node: 3, start: 20, end: 30, thread: 9),
                     Span(node: 2, start: 15, end: 40, thread: 9),
                     Span(node: 2, start: 70, end: 80, thread: 9),
                     Span(node: 2, start: 0, end: 10, thread: 11, sequence: 3)]);

        var timeline = builder.Build(nodes);

        var tree = new CallStackTree();

        var session = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x10 }, 1);
        var statement = tree.AddCall(session, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var task = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);
        var producer = tree.AddCall(task, new CallstackFrame { Module = "sqlmin", Rva = 0x40 }, 1);

        timeline.MapNodes([session, statement, task, producer]);

        var rooted = timeline.RootedAt(n => ReferenceEquals(n, statement));

        Assert.NotNull(rooted);

        Assert.Equal([7u, 9u], rooted.Threads.Select(t => t.ThreadId));

        var worker = rooted.Threads[1];

        Assert.Equal(2, worker.Calls);
        Assert.Equal([2, 3], worker.Rows.Select(r => r.NodeAt(0)));
    }

    [Fact]
    public void A_Root_That_Was_Never_Called_Gives_Nothing()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 10));

        Assert.Null(timeline.RootedAt(_ => false));
    }

    [Fact]
    public void Positions_Order_By_Sequence_Then_Steps()
    {
        Assert.True(TimeTravelTimeline.PositionOf(5, 1, 1000) > TimeTravelTimeline.PositionOf(5, 0, 1000));
        Assert.True(TimeTravelTimeline.PositionOf(6, 0, 1000) > TimeTravelTimeline.PositionOf(5, 999, 1000));
    }

    [Fact]
    public void Steps_Within_A_Sequence_Are_Spread_Evenly()
    {
        var timeline = Build(Span(node: 0, start: 100_000, end: 100_010),
                             Span(node: 0, start: 100_020, end: 100_030),
                             Span(node: 0, start: 999_990, end: 999_999));

        var starts = timeline.Threads[0].Rows[0].Starts(TimeTravelTimelineAxis.Position).ToArray();

        var step = timeline.StepOf(TimeTravelTimelineAxis.Position);

        Assert.Equal(20, (starts[1] - starts[0]) / step, 6);
        Assert.Equal(899_990, (starts[2] - starts[0]) / step, 3);
    }

    [Fact]
    public void Memory_Allocated_During_A_Call_Is_The_Sum_Of_The_Allocations_Inside_It()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 10));

        timeline.SetMemory([new(7, 10, 15, 100, 0xA), new(7, 12, 13, 40, 0xB), new(7, 20, 25, 50, 0xC), new(9, 10, 11, 5, 0xD)], []);

        Assert.Equal((150ul, 2), timeline.AllocatedDuring(7, 0, 30));
        Assert.Equal((50ul, 1), timeline.AllocatedDuring(7, 15, 30));
        Assert.Equal((100ul, 1), timeline.AllocatedDuring(7, 10, 15));
        Assert.Equal((0ul, 0), timeline.AllocatedDuring(8, 0, 30));
    }

    [Fact]
    public void A_Filtered_Timeline_Keeps_Its_Allocations()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 10));

        timeline.SetMemory([new(7, 1, 2, 64, 0xA)], []);

        Assert.Equal((64ul, 1), timeline.Where(_ => true).AllocatedDuring(7, 0, 5));
    }

    [Fact]
    public void Self_Allocations_Belong_To_The_Deepest_Call_Running_At_The_Time()
    {
        var timeline = Build(Span(node: 2, start: 10, end: 20),
                             Span(node: 1, start: 5, end: 30),
                             Span(node: 0, start: 0, end: 40));

        var rows = timeline.Threads[0].Rows;

        var outer = rows[0].Span(TimeTravelTimelineAxis.Position, 0);
        var middle = rows[1].Span(TimeTravelTimelineAxis.Position, 0);
        var inner = rows[2].Span(TimeTravelTimelineAxis.Position, 0);

        var inMiddle = (inner.End + middle.End) / 2;
        var inOuter = (middle.End + outer.End) / 2;

        timeline.SetMemory([new(7, inner.Start, inner.Start + 1e-6, 100, 0xA),
                            new(7, inMiddle, inMiddle + 1e-6, 30, 0xB),
                            new(7, inOuter, inOuter + 1e-6, 50, 0xC)],
                           []);

        Assert.Equal([(0, 50ul), (1, 30ul), (2, 100ul)],
                     timeline.SelfAllocations().OrderBy(s => s.Depth).Select(s => (s.Depth, s.Bytes)));
    }

    private static TimeTravelTimeline Build(params TimeTravelCallSpan[] spans)
    {
        var builder = new TimeTravelTimeline.Builder();

        builder.Add(spans);

        return builder.Build(Nodes);
    }

    private static TimeTravelCallSpan Span(int node,
                                           ulong start,
                                           ulong end,
                                           uint thread = 7,
                                           ulong sequence = 1,
                                           uint call = 0,
                                           uint flags = 1)
        => new(sequence, start, sequence, end, start, end, node, thread, call, flags);
}
