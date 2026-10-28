using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline;

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
        var timeline = Build(Span(callNodeIndex: 2, start: 10, end: 20),
                             Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40));

        var thread = Assert.Single(timeline.Threads);

        Assert.Equal([0, 1, 2], thread.Rows.Select(r => r.GetCallNodeIndex(0)));
        Assert.Equal(3, thread.Calls);
    }

    [Fact]
    public void Calls_In_A_Row_Are_Ordered_By_Where_They_Start()
    {
        var timeline = Build(Span(callNodeIndex: 1, start: 50, end: 60, sequence: 9),
                             Span(callNodeIndex: 1, start: 10, end: 20, sequence: 3));

        var row = Assert.Single(timeline.Threads).Rows[1];

        Assert.Equal([10d, 50d], Starts(row));
    }

    [Fact]
    public void Instructions_Come_From_The_Thread_Clock()
    {
        var timeline = Build(new CallSpan(1, 0, 5, 20, 0, 1020, 0, 7, 0, 1),
                             new CallSpan(3, 40, 3, 60, 340, 360, 1, 7, 0, 1));

        var thread = Assert.Single(timeline.Threads);

        var outer = thread.Rows[0];

        var inner = thread.Rows[1];

        Assert.Equal(3, thread.Clock.Count);
        Assert.Equal((0ul, 1020ul), (outer.GetFrom(0).Instructions, outer.GetTo(0).Instructions));
        Assert.Equal((340ul, 360ul), (inner.GetFrom(0).Instructions, inner.GetTo(0).Instructions));
        Assert.Equal(0, inner.GetIndex(Instructions, 350, 0));
        Assert.Equal(-1, inner.GetIndex(Instructions, 361, 0));
    }

    [Fact]
    public void A_Point_At_A_Position_Takes_Its_Instructions_From_The_Thread_Clock()
    {
        var timeline = Build(new CallSpan(1, 0, 5, 20, 0, 1020, 0, 7, 0, 1),
                             new CallSpan(3, 40, 3, 60, 340, 360, 1, 7, 0, 1));

        var point = timeline.GetPoint(At(3, 50));

        Assert.Equal((At(3, 50), 350ul), (point.Position, point.Instructions));
    }

    [Fact]
    public void A_Call_Is_Found_By_Where_It_Ran()
    {
        var row = Build(Span(callNodeIndex: 0, start: 0, end: 10), Span(callNodeIndex: 0, start: 20, end: 30, sequence: 2)).Threads[0].Rows[0];

        Assert.Equal(1, row.GetIndex(Instructions, 25, 0));
        Assert.Equal(-1, row.GetIndex(Instructions, 15, 0));
        Assert.Equal(1, row.GetIndex(Instructions, 19.5, 1));
    }

    [Fact]
    public void The_Stack_At_A_Position_Is_Each_Threads_Calls_In_Progress_Outermost_First()
    {
        var timeline = Build(Span(callNodeIndex: 0, start: 0, end: 40, callIndex: 4),
                             Span(callNodeIndex: 1, start: 5, end: 30, callIndex: 5),
                             Span(callNodeIndex: 2, start: 10, end: 20, callIndex: 6),
                             Span(callNodeIndex: 0, start: 20, end: 60, thread: 9, callIndex: 7));

        var stacks = timeline.GetStack(At(1, 25));

        Assert.Equal([(7u, "0:4 1:5"), (9u, "0:7")],
                     stacks.Select(s => (s.ThreadId, string.Join(' ', s.Calls.Select(c => $"{c.CallNodeIndex}:{c.CallIndex}")))));
    }

    [Fact]
    public void A_Position_No_Call_Covers_Has_No_Stack()
        => Assert.Empty(Build(Span(callNodeIndex: 0, start: 0, end: 10)).GetStack(At(1, 15)));

    [Fact]
    public void Each_Thread_Has_Its_Own_Lane_In_The_Order_It_Started()
    {
        var timeline = Build(Span(callNodeIndex: 0, start: 0, end: 10, thread: 9, sequence: 5),
                             Span(callNodeIndex: 0, start: 0, end: 10, thread: 4, sequence: 2));

        Assert.Equal([4u, 9u], timeline.Threads.Select(t => t.ThreadId));
        Assert.Equal(10, timeline.GetEnd(Instructions));
    }

    [Fact]
    public void A_Call_Without_A_Logged_Call_Reads_As_No_Call()
    {
        var span = Build(Span(callNodeIndex: 0, start: 0, end: 10, callIndex: uint.MaxValue, flags: 2)).Threads[0].Rows[0].GetSpan(0);

        Assert.Equal(-1, span.CallIndex);
        Assert.Equal(TraceSpanFlags.StartUnknown, span.Flags);
    }

    [Fact]
    public void A_Filter_Drops_Calls_But_Keeps_Each_Lane_And_Row()
    {
        var timeline = Build(Span(callNodeIndex: 2, start: 10, end: 20),
                             Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40));

        var thread = Assert.Single(timeline.Where(n => n != 2).Threads);

        Assert.Equal([1, 1, 0], thread.Rows.Select(r => r.Count));
        Assert.Equal(2, thread.Depth);
        Assert.Equal(2, thread.Calls);
    }

    [Fact]
    public void Calls_Under_An_Excluded_Frame_Go_With_It()
    {
        var timeline = Build(Span(callNodeIndex: 2, start: 10, end: 20),
                             Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var publish = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);
        var inner = tree.AddCall(publish, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([root, publish, inner]);

        timeline.RemoveCallsUnder(n => ReferenceEquals(n, publish));

        var thread = Assert.Single(timeline.Threads);

        Assert.Equal([1, 0, 0], thread.Rows.Select(r => r.Count));
        Assert.Equal(1, thread.Calls);
    }

    [Fact]
    public void A_Cropped_Timeline_Fits_To_The_Calls_Under_Its_Root()
    {
        var timeline = Build(Span(callNodeIndex: 2, start: 10, end: 20),
                             Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var execute = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var inner = tree.AddCall(execute, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([null, execute, inner]);

        var cropped = timeline.CroppedTo(new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance) { execute });

        Assert.NotNull(cropped);

        var thread = Assert.Single(cropped.Threads);

        Assert.Equal([1, 2], thread.Rows.Select(r => r.GetCallNodeIndex(0)));
        Assert.Equal((5d, 30d), (cropped.GetStart(Instructions), cropped.GetEnd(Instructions)));
        Assert.Equal((0d, 40d), (timeline.GetStart(Instructions), timeline.GetEnd(Instructions)));
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

        builder.Add([Span(callNodeIndex: 0, start: 0, end: 100),
                     Span(callNodeIndex: 1, start: 10, end: 90),
                     Span(callNodeIndex: 2, start: 20, end: 40),
                     Span(callNodeIndex: 3, start: 25, end: 30)]);

        var timeline = builder.Build(nodes);

        var tree = new CallStackTree();

        var execute = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var scan = tree.AddCall(execute, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);
        var read = tree.AddCall(scan, new CallstackFrame { Module = "sqlmin", Rva = 0x40 }, 1);

        timeline.MapNodes([null, execute, scan, read]);

        var cropped = timeline.CroppedTo(new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance) { execute });

        Assert.NotNull(cropped);

        var thread = Assert.Single(cropped.Threads);

        Assert.Equal([1, 2, 3], thread.Rows.Select(r => r.GetCallNodeIndex(0)));
        Assert.Equal((10d, 90d), (cropped.GetStart(Instructions), cropped.GetEnd(Instructions)));
    }

    [Fact]
    public void A_Cropped_Row_Shares_The_Calls_It_Keeps_With_Its_Source_Row()
    {
        var (timeline, execute) = BuildAroundExecute();

        var cropped = timeline.CroppedTo(new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance) { execute });

        Assert.NotNull(cropped);

        var source = Assert.Single(timeline.Threads);

        var thread = Assert.Single(cropped.Threads);

        var rows = thread.Rows;

        Assert.Equal([[20d], [25d, 35d]], rows.Select(r => Starts(r)));
        Assert.Equal([2, 3, 3], rows.SelectMany(r => Enumerable.Range(0, r.Count).Select(r.GetCallNodeIndex)));
        Assert.Same(source.Store, thread.Store);
        Assert.Equal(0, rows.Sum(r => r.Bytes));
    }

    [Fact]
    public void A_Folded_Row_Reads_The_Calls_It_Merges_From_Its_Threads_Store()
    {
        var (timeline, _) = BuildAroundExecute();

        var source = Assert.Single(timeline.Threads);

        var thread = Assert.Single(timeline.Folded(n => n.Frame?.Rva == 0x10).Threads);

        var row = thread.Rows[0];

        Assert.Equal([2d, 20d, 72d], Starts(row));
        Assert.Equal([8d, 60d, 78d], Ends(row));
        Assert.Equal([1, 2, 1], Enumerable.Range(0, row.Count).Select(row.GetCallNodeIndex));
        Assert.Same(source.Store, thread.Store);
        Assert.Equal(3 * sizeof(int), row.Bytes);
        Assert.Equal(0, thread.Rows[1].Bytes);
    }

    [Fact]
    public void A_Filtered_Row_Reads_The_Calls_It_Keeps_From_Its_Threads_Store()
    {
        var (timeline, _) = BuildAroundExecute();

        var source = Assert.Single(timeline.Threads);

        var thread = Assert.Single(timeline.Where(n => n != 2).Threads);

        Assert.Equal([[0d, 70d], [2d, 25d, 35d, 72d]], thread.Rows.Select(r => Starts(r)));
        Assert.Equal([[10d, 80d], [8d, 30d, 40d, 78d]], thread.Rows.Select(r => Ends(r)));
        Assert.Same(source.Store, thread.Store);
        Assert.Same(source.Rows[1], thread.Rows[1]);
        Assert.Equal(2 * sizeof(int), thread.Rows[0].Bytes);
    }

    [Fact]
    public void Calls_Removed_From_Between_Kept_Calls_Close_Up_In_Their_Row()
    {
        var (timeline, execute) = BuildAroundExecute();

        timeline.RemoveCallsUnder(n => ReferenceEquals(n, execute));

        var thread = Assert.Single(timeline.Threads);

        Assert.Equal([[0d, 70d], [2d, 72d]], thread.Rows.Select(r => Starts(r)));
        Assert.Equal([[10d, 80d], [8d, 78d]], thread.Rows.Select(r => Ends(r)));
        Assert.Equal([0, 0, 1, 1], thread.Rows.SelectMany(r => Enumerable.Range(0, r.Count).Select(r.GetCallNodeIndex)));
        Assert.Equal(4, thread.Calls);
    }

    [Fact]
    public void An_Exported_Timeline_Reads_Each_Row_Back_Into_One_Store_Per_Thread()
    {
        var (timeline, execute) = BuildAroundExecute();

        timeline.RemoveCallsUnder(n => ReferenceEquals(n, execute));

        CallStackNode[] nodes = [.. Enumerable.Range(0, 4).Select(i => timeline.NodeOf(i)!)];

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            timeline.Write(writer, n => Array.IndexOf(nodes, n));
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var thread = Assert.Single(TraceTimeline.Read(reader, nodes).Threads);

        Assert.Equal([[0d, 70d], [2d, 72d]], thread.Rows.Select(r => Starts(r)));
        Assert.Equal([[10d, 80d], [8d, 78d]], thread.Rows.Select(r => Ends(r)));
        Assert.Equal([0, 0, 1, 1], thread.Rows.SelectMany(r => Enumerable.Range(0, r.Count).Select(r.GetCallNodeIndex)));
        Assert.Equal(4, thread.Calls);
        Assert.Equal(0, thread.Rows.Sum(r => r.Bytes));
    }

    [Fact]
    public void A_Shared_Row_Writes_Only_The_Calls_It_Keeps()
    {
        var (timeline, execute) = BuildAroundExecute();

        var cropped = timeline.CroppedTo(new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance) { execute });

        var thread = Assert.Single(cropped!.Threads);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            thread.Rows[1].Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var read = TraceTimelineRow.Read(reader, TraceScale.Default, thread.Clock, new TraceSpanStore(2), 0);

        Assert.Equal([25d, 35d], Starts(read));
        Assert.Equal([3, 3], Enumerable.Range(0, read.Count).Select(read.GetCallNodeIndex));
    }

    [Fact]
    public void A_Cropped_Timeline_Drops_Threads_Outside_The_Cropped_Tree()
    {
        var timeline = Build(Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40, thread: 9));

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
        var timeline = Build(Span(callNodeIndex: 2, start: 10, end: 20),
                             Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var folded = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);
        var inner = tree.AddCall(folded, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([root, folded, inner]);

        var thread = Assert.Single(timeline.Folded(n => ReferenceEquals(n, folded)).Threads);

        Assert.Equal([0, 2], thread.Rows.Select(r => r.GetCallNodeIndex(0)));
        Assert.Equal(2, thread.Calls);
    }

    [Fact]
    public void Calls_Lifted_Between_Kept_Calls_Stay_In_Start_Order()
    {
        var builder = new TraceTimeline.Builder();

        builder.Add([Span(callNodeIndex: 0, start: 0, end: 100),
                     Span(callNodeIndex: 3, start: 5, end: 8, callIndex: 7),
                     Span(callNodeIndex: 1, start: 10, end: 40),
                     Span(callNodeIndex: 2, start: 20, end: 30, callIndex: 8),
                     Span(callNodeIndex: 3, start: 50, end: 60, callIndex: 9)]);

        var timeline = builder.Build([new(-1, 0, 0x1000, 0, 1), new(0, 0, 0x2000, 0, 1), new(1, 0, 0x3000, 0, 1), new(0, 0, 0x4000, 0, 2)]);

        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10 }, 1);
        var folded = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x20 }, 1);
        var inner = tree.AddCall(folded, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);
        var sibling = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x40 }, 2);

        timeline.MapNodes([root, folded, inner, sibling]);

        var row = Assert.Single(timeline.Folded(n => ReferenceEquals(n, folded)).Threads).Rows[1];

        Assert.Equal([3, 2, 3], Enumerable.Range(0, row.Count).Select(row.GetCallNodeIndex));
        Assert.Equal([7, 8, 9], Enumerable.Range(0, row.Count).Select(row.GetCallIndex));
        Assert.Equal([5d, 20d, 50d], Starts(row));
    }

    [Fact]
    public void Overlapping_Calls_In_One_Row_Are_Sorted_With_Their_Columns()
    {
        var timeline = Build(Span(callNodeIndex: 0, start: 30, end: 40, callIndex: 1),
                             Span(callNodeIndex: 0, start: 10, end: 50, callIndex: 2),
                             Span(callNodeIndex: 0, start: 20, end: 25, callIndex: 3));

        var row = Assert.Single(timeline.Threads).Rows[0];

        Assert.Equal([10d, 20d, 30d], Starts(row));
        Assert.Equal([2, 3, 1], Enumerable.Range(0, row.Count).Select(row.GetCallIndex));
        Assert.Equal([50d, 25d, 40d], Enumerable.Range(0, row.Count).Select(i => row.GetEnd(Instructions, i)));
    }

    [Fact]
    public void Folding_A_Root_Lifts_Its_Callees_To_The_Top()
    {
        var timeline = Build(Span(callNodeIndex: 2, start: 10, end: 20),
                             Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var outer = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x10 }, 1);
        var execute = tree.AddCall(outer, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var inner = tree.AddCall(execute, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([outer, execute, inner]);

        var rooted = timeline.RootedAt(n => ReferenceEquals(n, execute));

        Assert.NotNull(rooted);

        var thread = Assert.Single(rooted.Folded(n => ReferenceEquals(n, outer) || ReferenceEquals(n, execute)).Threads);

        Assert.Equal([2], thread.Rows.Select(r => r.GetCallNodeIndex(0)));
    }

    [Fact]
    public void Each_Call_Path_Knows_The_Path_It_Was_Called_From()
    {
        var timeline = Build(Span(callNodeIndex: 0, start: 0, end: 10));

        Assert.Equal(3, timeline.NodeCount);
        Assert.Equal([-1, 0, 1], Enumerable.Range(0, 3).Select(timeline.ParentOf));
    }

    [Fact]
    public void A_Root_Keeps_Only_Its_Calls_And_Their_Callees_With_Itself_At_The_Top()
    {
        var timeline = Build(Span(callNodeIndex: 2, start: 10, end: 20),
                             Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40));

        var tree = new CallStackTree();

        var outer = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x10 }, 1);
        var execute = tree.AddCall(outer, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var inner = tree.AddCall(execute, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([outer, execute, inner]);

        var rooted = timeline.RootedAt(n => ReferenceEquals(n, execute));

        Assert.NotNull(rooted);

        var thread = Assert.Single(rooted.Threads);

        Assert.Equal([1, 2], thread.Rows.Select(r => r.GetCallNodeIndex(0)));
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

        builder.Add([Span(callNodeIndex: 1, start: 10, end: 50),
                     Span(callNodeIndex: 0, start: 0, end: 60),
                     Span(callNodeIndex: 3, start: 20, end: 30, thread: 9),
                     Span(callNodeIndex: 2, start: 15, end: 40, thread: 9),
                     Span(callNodeIndex: 2, start: 70, end: 80, thread: 9),
                     Span(callNodeIndex: 2, start: 0, end: 10, thread: 11, sequence: 3)]);

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
        Assert.Equal([2, 3], worker.Rows.Select(r => r.GetCallNodeIndex(0)));
    }

    [Fact]
    public void A_Root_That_Was_Never_Called_Gives_Nothing()
    {
        var timeline = Build(Span(callNodeIndex: 0, start: 0, end: 10));

        Assert.Null(timeline.RootedAt(_ => false));
    }

    [Fact]
    public void Positions_Order_By_Sequence_Then_Steps()
    {
        var scale = new TraceScale(1000);

        Assert.True(scale.GetValue(At(5, 1)) > scale.GetValue(At(5, 0)));
        Assert.True(scale.GetValue(At(6, 0)) > scale.GetValue(At(5, 999)));
        Assert.Equal(At(5, 999), scale.GetPosition(scale.GetValue(At(5, 999))));
    }

    [Fact]
    public void Steps_Within_A_Sequence_Are_Spread_Evenly()
    {
        var timeline = Build(Span(callNodeIndex: 0, start: 100_000, end: 100_010),
                             Span(callNodeIndex: 0, start: 100_020, end: 100_030),
                             Span(callNodeIndex: 0, start: 999_990, end: 999_999));

        var starts = Starts(timeline.Threads[0].Rows[0], TraceTimelineAxis.Position);

        var step = timeline.Scale.GetStep(TraceTimelineAxis.Position);

        Assert.Equal(20, (starts[1] - starts[0]) / step, 6);
        Assert.Equal(899_990, (starts[2] - starts[0]) / step, 3);
    }

    [Fact]
    public void Self_Allocations_Belong_To_The_Deepest_Call_Running_At_The_Time()
    {
        var timeline = Build(Span(callNodeIndex: 2, start: 10, end: 20),
                             Span(callNodeIndex: 1, start: 5, end: 30),
                             Span(callNodeIndex: 0, start: 0, end: 40));

        var memory = MemoryIndex.Build([new(7, At(1, 10), At(1, 11), 100, 0xA),
                                        new(7, At(1, 25), At(1, 26), 30, 0xB),
                                        new(7, At(1, 35), At(1, 36), 50, 0xC)],
                                       []);

        Assert.Equal([(0, 50ul), (1, 30ul), (2, 100ul)],
                     timeline.GetSelfAllocations(memory).OrderBy(s => s.Depth).Select(s => (s.Depth, s.Bytes)));
    }

    [Fact]
    public void Call_Activity_Counts_Each_Call_In_The_Bucket_Where_It_Starts()
    {
        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x100 }, 3);
        var child = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x200 }, 2);

        var timeline = Build(Span(callNodeIndex: 0, start: 0, end: 10),
                             Span(callNodeIndex: 0, start: 80, end: 85),
                             Span(callNodeIndex: 0, start: 90, end: 100),
                             Span(callNodeIndex: 1, start: 30, end: 35),
                             Span(callNodeIndex: 1, start: 31, end: 32, flags: 3));

        timeline.MapNodes([root, child, null]);

        var activity = timeline.GetCallActivity(4);

        Assert.Equal([1, 0, 0, 2], activity[root]);
        Assert.Equal([0, 1, 0, 0], activity[child]);
    }

    [Fact]
    public void A_Call_Is_Found_Where_The_Call_Log_Says_It_Started()
    {
        CallSpan[] spans = [Span(callNodeIndex: 0, start: 0, end: 40), Span(callNodeIndex: 1, start: 5, end: 20), Span(callNodeIndex: 1, start: 25, end: 30, callIndex: 1)];

        var timeline = Build(spans);

        var nodes = MapNodes(timeline);

        timeline.SetCalls(TraceCallRows.CallsFor(Nodes, spans));

        var found = timeline.FindCall(nodes[1], 1);

        Assert.Equal((0, 1, 1, 1), (found?.ThreadIndex, found?.Depth, found?.Index, found?.CallIndex));
        Assert.Same(nodes[1], found?.Node);
    }

    [Fact]
    public void A_Call_Is_Not_Found_Without_Its_Call_Log_Or_Outside_It()
    {
        CallSpan[] spans = [Span(callNodeIndex: 0, start: 0, end: 40)];

        var timeline = Build(spans);

        var nodes = MapNodes(timeline);

        Assert.Null(timeline.FindCall(nodes[0], 0));

        timeline.SetCalls(TraceCallRows.CallsFor(Nodes, spans));

        Assert.NotNull(timeline.FindCall(nodes[0], 0));
        Assert.Null(timeline.FindCall(nodes[0], 1));
        Assert.Null(timeline.FindCall(nodes[2], 0));
    }

    private static CallStackNode[] MapNodes(TraceTimeline timeline)
    {
        var tree = new CallStackTree();

        var root = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqlmin", Rva = 0x10, Address = 0x1000 }, 1);

        var child = tree.AddCall(root, new CallstackFrame { Module = "sqlmin", Rva = 0x20, Address = 0x2000 }, 1);

        var leaf = tree.AddCall(child, new CallstackFrame { Module = "sqlmin", Rva = 0x30, Address = 0x3000 }, 1);

        CallStackNode[] nodes = [root, child, leaf];

        timeline.MapNodes(nodes);

        return nodes;
    }

    private static (TraceTimeline Timeline, CallStackNode Execute) BuildAroundExecute()
    {
        CallNode[] nodes =
        [
            new(-1, 0, 0x1000, 0, 1),
            new(0, 0, 0x2000, 0, 1),
            new(-1, 0, 0x3000, 0, 1),
            new(2, 0, 0x4000, 0, 1)
        ];

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(callNodeIndex: 0, start: 0, end: 10),
                     Span(callNodeIndex: 1, start: 2, end: 8),
                     Span(callNodeIndex: 2, start: 20, end: 60),
                     Span(callNodeIndex: 3, start: 25, end: 30),
                     Span(callNodeIndex: 3, start: 35, end: 40),
                     Span(callNodeIndex: 0, start: 70, end: 80),
                     Span(callNodeIndex: 1, start: 72, end: 78)]);

        var timeline = builder.Build(nodes);

        var tree = new CallStackTree();

        var outside = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x10 }, 1);
        var setup = tree.AddCall(outside, new CallstackFrame { Module = "sqllang", Rva = 0x18 }, 1);
        var execute = tree.AddCall(tree.Root, new CallstackFrame { Module = "sqllang", Rva = 0x20 }, 1);
        var scan = tree.AddCall(execute, new CallstackFrame { Module = "sqlmin", Rva = 0x30 }, 1);

        timeline.MapNodes([outside, setup, execute, scan]);

        return (timeline, execute);
    }

    private static double[] Starts(TraceTimelineRow row, TraceTimelineAxis axis = Instructions)
        => [.. Enumerable.Range(0, row.Count).Select(i => row.GetStart(axis, i))];

    private static double[] Ends(TraceTimelineRow row) => [.. Enumerable.Range(0, row.Count).Select(i => row.GetEnd(Instructions, i))];

    private static TraceTimeline Build(params CallSpan[] spans)
    {
        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        return builder.Build(Nodes);
    }

    private static CallSpan Span(int callNodeIndex,
                                 ulong start,
                                 ulong end,
                                 uint thread = 7,
                                 ulong sequence = 1,
                                 uint callIndex = 0,
                                 uint flags = 1)
        => new(sequence, start, sequence, end, start, end, callNodeIndex, thread, callIndex, flags);
}
