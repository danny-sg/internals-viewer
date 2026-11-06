using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRowHelper;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.Query.FullTrace.Tests.Timeline.Tracks;

[Trait("Category", "Unit")]
public class TraceRowReturnMarkMapTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong OpenFunction = 0x1100;

    private const ulong JoinIterator = 0x1F4_0000_1000;

    private const ulong ScanIterator = 0x1F4_0000_2000;


    private const ulong RowStatus = 0;

    private const ulong EndStatus = 0x40EC6;

    private static readonly ExecutionOperatorEvent Join = new()
    {
        Name = "Merge Join",
        OperatorDescription = "Merge Join",
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 0)
    };

    private static readonly ExecutionOperatorEvent Scan = new()
    {
        Name = "Index Scan",
        OperatorDescription = "Index Scan",
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 1),
        ParentNodeId = 0
    };

    private static readonly ExecutionOperatorEvent BlockingInput = new()
    {
        Name = "Index Scan",
        OperatorDescription = "Index Scan",
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 1),
        ParentNodeId = 0,
        IsBlockingInput = true
    };

    private static readonly CallNode[] Nodes =
    [
        new(-1, 0, Helper, JoinIterator, 2),
        new(0, 0, Helper, ScanIterator, 3)
    ];

    [Fact]
    public void A_Child_Row_Is_Passed_Up_Only_When_Its_Parent_Returns_A_Row_Before_The_Next_Child_Call()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        Assert.Equal((1, 1, 1),
                     (marks.GetCount(Scan, Thread, RowReturnType.Returned),
                      marks.GetCount(Scan, Thread, RowReturnType.Skipped),
                      marks.GetCount(Scan, Thread, RowReturnType.End)));
    }

    [Fact]
    public void Rows_Returned_By_An_Operator_With_No_Parent_Operator_Are_Passed_Up()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (55, 60, EndStatus)]);

        Assert.Equal((1, 0, 1),
                     (marks.GetCount(Join, Thread, RowReturnType.Returned),
                      marks.GetCount(Join, Thread, RowReturnType.Skipped),
                      marks.GetCount(Join, Thread, RowReturnType.End)));
    }

    [Fact]
    public void Rows_Read_By_A_Parent_That_Returns_Later_Are_Not_Passed_Up()
    {
        var marks = Build([(10, 100, RowStatus), (110, 120, RowStatus), (130, 140, EndStatus)],
                          [(12, 15, RowStatus), (20, 25, RowStatus), (30, 35, EndStatus)]);

        Assert.Equal((0, 2),
                     (marks.GetCount(Scan, Thread, RowReturnType.Returned), marks.GetCount(Scan, Thread, RowReturnType.Skipped)));
    }

    [Fact]
    public void Rows_A_Blocking_Operator_Reads_Before_Returning_Are_Returned()
    {
        var marks = Build([(10, 100, RowStatus), (110, 120, RowStatus), (130, 140, EndStatus)],
                          [(12, 15, RowStatus), (20, 25, RowStatus), (30, 35, EndStatus)],
                          bottom: BlockingInput);

        Assert.Equal((2, 0),
                     (marks.GetCount(BlockingInput, Thread, RowReturnType.Returned),
                      marks.GetCount(BlockingInput, Thread, RowReturnType.Skipped)));
    }

    [Fact]
    public void Batches_A_Blocking_Operator_Reads_Inside_Its_Open_Are_Returned()
    {
        var marks = Build([(50, 60, RowStatus), (70, 80, EndStatus)],
                          [(12, 15, RowStatus), (20, 25, RowStatus), (30, 35, EndStatus)],
                          childMethod: "BpGetNextBatch",
                          joinOpens: [(10, 40)],
                          bottom: BlockingInput);

        Assert.Equal((2, 0),
                     (marks.GetCount(BlockingInput, Thread, RowReturnType.Returned),
                      marks.GetCount(BlockingInput, Thread, RowReturnType.Skipped)));
    }

    [Fact]
    public void Rows_A_Filter_Reads_Without_Returning_Are_Rejected()
    {
        var filter = new ExecutionOperatorEvent
        {
            Name = "Filter",
            OperatorDescription = "Filter",
            PlanNodeIdentifier = new PlanNodeIdentifier(1, 0)
        };

        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)],
                          [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)],
                          top: filter);

        Assert.Equal((1, 1, 0),
                     (marks.GetCount(Scan, Thread, RowReturnType.Returned),
                      marks.GetCount(Scan, Thread, RowReturnType.Rejected),
                      marks.GetCount(Scan, Thread, RowReturnType.Skipped)));
    }

    [Fact]
    public void A_Batch_Passed_Up_Is_Marked_On_A_Batch_Operator()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)],
                          [(12, 15, RowStatus), (55, 60, EndStatus)],
                          childMethod: "BpGetNextBatch");

        Assert.Equal((true, false, 1), (marks.IsBatch(Scan), marks.IsBatch(Join), marks.GetCount(Scan, Thread, RowReturnType.Returned)));
    }

    [Fact]
    public void Each_GetRow_Call_Is_Linked_To_The_Operator_That_Made_It()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        var calls = marks.Ticks.GetTicks(Scan, Thread)!.All.OrderBy(t => t.Span.From.Position).ToList();

        Assert.All(calls, t => Assert.Same(Join, t.Caller));
        Assert.Equal([12ul, 20ul, 55ul], calls.Select(t => t.Span.From.Instructions));
        Assert.Equal([15ul, 25ul, 60ul], calls.Select(t => t.Span.To.Instructions));
        Assert.All(marks.Ticks.GetTicks(Join, Thread)!.All, t => Assert.Null(t.Caller));
    }

    [Fact]
    public void Rows_Read_Inside_An_Operators_Open_Are_Called_By_It_But_Not_Passed_Up()
    {
        var marks = Build([(50, 60, RowStatus), (70, 80, EndStatus)],
                          [(12, 15, RowStatus), (20, 25, RowStatus), (30, 35, EndStatus)],
                          joinOpens: [(10, 40)]);

        Assert.Equal((0, 2),
                     (marks.GetCount(Scan, Thread, RowReturnType.Returned), marks.GetCount(Scan, Thread, RowReturnType.Skipped)));
        Assert.All(marks.Ticks.GetTicks(Scan, Thread)!.All, t => Assert.Same(Join, t.Caller));
    }

    [Fact]
    public void The_Nearest_Mark_Is_The_Call_That_Returned()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        var mark = marks.Ticks.GetTicks(Scan, Thread)!.GetNearest(TraceTimelineAxis.Instructions, 24, 0);

        Assert.Equal((1, (byte)RowReturnType.Returned), (mark?.CallIndex, mark?.Value));
    }

    [Fact]
    public void A_Point_Inside_A_GetRow_Call_Finds_That_Call()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        var ticks = marks.Ticks.GetTicks(Scan, Thread)!;

        var call = ticks.GetNearest(TraceTimelineAxis.Instructions, 22, 0);

        Assert.Equal((1, 20ul, 25ul), (call?.CallIndex, call?.Span.From.Instructions, call?.Span.To.Instructions));
        Assert.Null(ticks.GetNearest(TraceTimelineAxis.Instructions, 17, 0));
    }

    [Fact]
    public void Stepping_Lands_On_The_End_Of_The_Next_Or_Previous_GetRow_Call()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        var ticks = marks.Ticks.GetTicks(Scan, Thread)!;

        const TraceTimelineAxis axis = TraceTimelineAxis.Instructions;

        var next = (ticks.GetNextEnd(axis, 15), ticks.GetNextEnd(axis, 25), ticks.GetNextEnd(axis, 60));

        var previous = (ticks.GetPreviousEnd(axis, 25), ticks.GetPreviousEnd(axis, 58), ticks.GetPreviousEnd(axis, 15));

        Assert.Equal<(double?, double?, double?)>((25, 60, null), next);
        Assert.Equal<(double?, double?, double?)>((15, 25, null), previous);
    }

    [Fact]
    public void Operators_Are_Requested_While_A_GetRow_Call_To_Them_Is_Running()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        Assert.Equal([Join, Scan], marks.GetRequested(At(13)).OrderBy(o => o.PlanNodeIdentifier?.NodeId));
        Assert.Equal([Join], marks.GetRequested(At(30)));
        Assert.Empty(marks.GetRequested(At(45)));
    }

    [Fact]
    public void Operators_Have_Returned_When_A_GetRow_Call_Other_Than_The_End_Returns_In_The_Range()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        Assert.Equal([Scan], marks.GetReturned(At(14), At(30)));
        Assert.Equal([Join], marks.GetReturned(At(30), At(45)));
        Assert.Empty(marks.GetReturned(At(15), At(20)));
        Assert.Empty(marks.GetReturned(At(45), At(80)));
    }

    private static TraceRowReturnMarkMap Build((ulong Start, ulong End, ulong Status)[] joinCalls,
                                             (ulong Start, ulong End, ulong Status)[] scanCalls,
                                             ExecutionOperatorEvent? top = null,
                                             string childMethod = "GetRowOrReQualifyHelper",
                                             (ulong Start, ulong End)[]? joinOpens = null,
                                             ExecutionOperatorEvent? bottom = null)
    {
        CallSpan[] spans = [.. joinCalls.Select((c, i) => Span(0, c.Start, c.End, (uint)i)),
                            .. scanCalls.Select((c, i) => Span(1, c.Start, c.End, (uint)i))];

        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        var timeline = builder.Build(Nodes);

        var tree = new CallTree();

        var join = tree.AddCall(tree.Root, Frame(JoinIterator), 1);

        var scan = tree.AddCall(join, Frame(ScanIterator, childMethod), 1);

        CallTreeNode[] nodes = [join, scan];

        timeline.MapNodes(nodes);

        var open = tree.AddCall(tree.Root, Frame(JoinIterator, "Open"), 1);

        var store = new TraceCallStore.Builder();

        store.Add(Helper, JoinIterator, Chunk([.. joinCalls.Select(c => Call(c, callNodeIndex: 0))]), joinCalls.Length);

        store.Add(Helper, ScanIterator, Chunk([.. scanCalls.Select(c => Call(c, callNodeIndex: 1))]), scanCalls.Length);

        if (joinOpens is not null)
        {
            ulong[][] opens = [.. joinOpens.Select(c => Call((c.Start, c.End, RowStatus), callNodeIndex: 2))];

            store.Add(OpenFunction, JoinIterator, Chunk(opens), opens.Length);
        }

        var calls = store.Build();

        calls.MapNodes([join, scan, open]);

        timeline.SetCalls(calls);

        TraceOperatorLifetime[] lifetimes =
        [
            new(Thread, top ?? Join, 0, [Whole()], MemoryUsageCurve.Empty, timeline.Scale) { Instances = [JoinIterator] },
            new(Thread, bottom ?? Scan, 1, [Whole()], MemoryUsageCurve.Empty, timeline.Scale) { Instances = [ScanIterator] }
        ];

        return TraceRowReturnMarkMap.Build(timeline, calls, new TraceOperatorLifetimeList(lifetimes));
    }

    private static ulong[] Call((ulong Start, ulong End, ulong Status) call, int callNodeIndex)
        => Row(call.Start, flags: Thread | TraceCallChunk.ReturnedFlag, returnValue: call.Status, callNodeIndex: callNodeIndex, endSequence: call.End);

    private static CallFrame Frame(ulong instance, string method = "GetRowOrReQualifyHelper") => new()
    {
        Module = new("sqlmin"),
        Rva = 0x10,
        Address = Helper,
        InstanceAddress = instance,
        Function = new FrameFunction
        {
            ClassName = "CQScanNew",
            MethodName = method,
            SymbolCategory = SymbolCategory.Unknown
        }
    };

    private static TraceSpan Whole() => new(new TracePoint(At(0), 0), new TracePoint(At(1000), 1000), 0, 0, TraceSpanFlags.None);

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end, uint callIndex)
        => new(start, 0, end, 0, start, end, callNodeIndex, Thread, callIndex, 1);
}
