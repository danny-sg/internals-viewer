using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceTimelineTests
{
    private const TraceTimelineAxis Instructions = TraceTimelineAxis.Instructions;

    private static readonly CallNode[] Nodes =
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
    public void The_Stack_At_A_Position_Is_Each_Threads_Calls_In_Progress_Outermost_First()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 40, call: 4),
                             Span(node: 1, start: 5, end: 30, call: 5),
                             Span(node: 2, start: 10, end: 20, call: 6),
                             Span(node: 0, start: 0, end: 40, thread: 9, sequence: 2, call: 7));

        var stacks = timeline.StackAt(Instructions, 25);

        Assert.Equal([(7u, "0:4 1:5"), (9u, "0:7")],
                     stacks.Select(s => (s.ThreadId, string.Join(' ', s.Calls.Select(c => $"{c.Node}:{c.Call}")))));
    }

    [Fact]
    public void A_Position_No_Call_Covers_Has_No_Stack()
        => Assert.Empty(Build(Span(node: 0, start: 0, end: 10)).StackAt(Instructions, 15));

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
        Assert.Equal(TraceSpanFlags.StartUnknown, span.Flags);
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
    public void A_Cropped_Timeline_Fits_To_The_Calls_Under_Its_Root()
    {
        var timeline = Build(Span(node: 2, start: 10, end: 20),
                             Span(node: 1, start: 5, end: 30),
                             Span(node: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var execute = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var inner = tree.AddCall(execute, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([null, execute, inner]);

        var cropped = timeline.CroppedTo(new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance) { execute });

        Assert.NotNull(cropped);

        var thread = Assert.Single(cropped.Threads);

        Assert.Equal([1, 2], thread.Rows.Select(r => r.NodeAt(0)));
        Assert.Equal((5d, 30d), (cropped.StartOf(Instructions), cropped.EndOf(Instructions)));
        Assert.Equal((0d, 40d), (timeline.StartOf(Instructions), timeline.EndOf(Instructions)));
    }

    [Fact]
    public void A_Cropped_Timeline_Keeps_Calls_After_A_Recording_Gap_Where_The_Tree_Grafted_Them()
    {
        CallNode[] nodes =
        [
            new(-1, 0, 0x1000, 0, 1),
            new(0, 0, 0x2000, 0, 1),
            new(-1, 0, 0x3000, 0, 1),
            new(2, 0, 0x4000, 0, 1)
        ];

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(node: 0, start: 0, end: 100),
                     Span(node: 1, start: 10, end: 90),
                     Span(node: 2, start: 20, end: 40),
                     Span(node: 3, start: 25, end: 30)]);

        var timeline = builder.Build(nodes);

        var tree = new CallStackTree();

        var execute = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var scan = tree.AddCall(execute, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);
        var read = tree.AddCall(scan, new CallstackFrame { Module = "sqlmin", Rva = 0x40 }, 1);

        timeline.MapNodes([null, execute, scan, read]);

        var cropped = timeline.CroppedTo(new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance) { execute });

        Assert.NotNull(cropped);

        var thread = Assert.Single(cropped.Threads);

        Assert.Equal([1, 2, 3], thread.Rows.Select(r => r.NodeAt(0)));
        Assert.Equal((10d, 90d), (cropped.StartOf(Instructions), cropped.EndOf(Instructions)));
    }

    [Fact]
    public void A_Cropped_Timeline_Drops_Threads_Outside_The_Cropped_Tree()
    {
        var timeline = Build(Span(node: 1, start: 5, end: 30),
                             Span(node: 0, start: 0, end: 40, thread: 9));

        var tree = new CallStackTree();

        var execute = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);

        var uncropped = new CallStackTree();

        var elsewhere = uncropped.AddCall(uncropped.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x90 }, 1);

        timeline.MapNodes([elsewhere, execute, null]);

        var cropped = timeline.CroppedTo(new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance) { execute });

        Assert.NotNull(cropped);
        Assert.Equal([7u], cropped.Threads.Select(t => t.ThreadId));
    }

    [Fact]
    public void Calls_Under_A_Folded_Frame_Move_Up_Into_Its_Place()
    {
        var timeline = Build(Span(node: 2, start: 10, end: 20),
                             Span(node: 1, start: 5, end: 30),
                             Span(node: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var folded = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);
        var inner = tree.AddCall(folded, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([root, folded, inner]);

        var thread = Assert.Single(timeline.Folded(n => ReferenceEquals(n, folded)).Threads);

        Assert.Equal([0, 2], thread.Rows.Select(r => r.NodeAt(0)));
        Assert.Equal(2, thread.Calls);
    }

    [Fact]
    public void Folding_A_Root_Lifts_Its_Callees_To_The_Top()
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

        var thread = Assert.Single(rooted.Folded(n => ReferenceEquals(n, outer) || ReferenceEquals(n, execute)).Threads);

        Assert.Equal([2], thread.Rows.Select(r => r.NodeAt(0)));
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
        CallNode[] nodes =
        [
            new(-1, 0, 0x1000, 0, 1),
            new(0, 0, 0x2000, 0, 1),
            new(-1, 0, 0x3000, 0, 2),
            new(2, 0, 0x4000, 0, 1)
        ];

        var builder = new TraceTimeline.Builder();

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
        Assert.True(TraceTimeline.PositionOf(5, 1, 1000) > TraceTimeline.PositionOf(5, 0, 1000));
        Assert.True(TraceTimeline.PositionOf(6, 0, 1000) > TraceTimeline.PositionOf(5, 999, 1000));
    }

    [Fact]
    public void Steps_Within_A_Sequence_Are_Spread_Evenly()
    {
        var timeline = Build(Span(node: 0, start: 100_000, end: 100_010),
                             Span(node: 0, start: 100_020, end: 100_030),
                             Span(node: 0, start: 999_990, end: 999_999));

        var starts = timeline.Threads[0].Rows[0].Starts(TraceTimelineAxis.Position).ToArray();

        var step = timeline.StepOf(TraceTimelineAxis.Position);

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

        var outer = rows[0].Span(TraceTimelineAxis.Position, 0);
        var middle = rows[1].Span(TraceTimelineAxis.Position, 0);
        var inner = rows[2].Span(TraceTimelineAxis.Position, 0);

        var inMiddle = (inner.End + middle.End) / 2;
        var inOuter = (middle.End + outer.End) / 2;

        timeline.SetMemory([new(7, inner.Start, inner.Start + 1e-6, 100, 0xA),
                            new(7, inMiddle, inMiddle + 1e-6, 30, 0xB),
                            new(7, inOuter, inOuter + 1e-6, 50, 0xC)],
                           []);

        Assert.Equal([(0, 50ul), (1, 30ul), (2, 100ul)],
                     timeline.SelfAllocations().OrderBy(s => s.Depth).Select(s => (s.Depth, s.Bytes)));
    }

    [Fact]
    public void Call_Activity_Counts_Each_Call_In_The_Bucket_Where_It_Starts()
    {
        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x100 }, 3);
        var child = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x200 }, 2);

        var timeline = Build(Span(node: 0, start: 0, end: 10),
                             Span(node: 0, start: 80, end: 85),
                             Span(node: 0, start: 90, end: 100),
                             Span(node: 1, start: 30, end: 35),
                             Span(node: 1, start: 31, end: 32, flags: 3));

        timeline.MapNodes([root, child, null]);

        timeline.AddCallActivity(4);

        Assert.Equal([1, 0, 0, 2], root.CallActivity);
        Assert.Equal([0, 1, 0, 0], child.CallActivity);
    }

    [Fact]
    public void Event_Link_Of_A_Linked_Event_Is_Its_Own_Link()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 40));

        LatchEvent[] latches = [new() { SequenceId = 1 }, new() { SequenceId = 2 }];

        timeline.SetEventLinks([new TraceEventLink(latches[0], null, 7, 10, 10), new TraceEventLink(latches[1], null, 7, 20, 20)]);

        Assert.Equal(20d, timeline.EventLinkOf(latches[1])?.Position);
        Assert.Null(timeline.EventLinkOf(new LatchEvent { SequenceId = 3 }));
    }

    [Fact]
    public void Event_Link_Of_A_Group_Is_The_Link_Of_An_Event_It_Owns()
    {
        var timeline = Build(Span(node: 0, start: 0, end: 40));

        var latch = new LatchEvent { SequenceId = 1 };

        timeline.SetEventLinks([new TraceEventLink(latch, null, 7, 10, 10)]);

        Assert.Same(latch, timeline.EventLinkOf(new ReadEventGroup { SequenceId = 2, Events = [latch] })?.Event);
    }

    private static TraceTimeline Build(params CallSpan[] spans)
    {
        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        return builder.Build(Nodes);
    }

    private static CallSpan Span(int node,
                                 ulong start,
                                 ulong end,
                                 uint thread = 7,
                                 ulong sequence = 1,
                                 uint call = 0,
                                 uint flags = 1)
        => new(sequence, start, sequence, end, start, end, node, thread, call, flags);
}
