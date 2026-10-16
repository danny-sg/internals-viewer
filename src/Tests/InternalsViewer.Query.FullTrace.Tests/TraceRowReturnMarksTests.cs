using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceRowReturnMarksTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong JoinIterator = 0x1F4_0000_1000;

    private const ulong ScanIterator = 0x1F4_0000_2000;

    private const ulong RowStatus = 0;

    private const ulong EndStatus = 0x40EC6;

    private static readonly ExecutionOperatorEvent Join = new()
    {
        Name = "Merge Join",
        OperatorDescription = "Merge Join",
        Instances = [JoinIterator]
    };

    private static readonly ExecutionOperatorEvent Scan = new()
    {
        Name = "Index Scan",
        OperatorDescription = "Index Scan",
        Instances = [ScanIterator]
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
                     (marks.GetCount(Scan, Thread, RowReturnType.PassedUp),
                      marks.GetCount(Scan, Thread, RowReturnType.NotPassedUp),
                      marks.GetCount(Scan, Thread, RowReturnType.End)));
    }

    [Fact]
    public void Rows_Returned_By_An_Operator_With_No_Parent_Operator_Are_Passed_Up()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (55, 60, EndStatus)]);

        Assert.Equal((1, 0, 1),
                     (marks.GetCount(Join, Thread, RowReturnType.PassedUp),
                      marks.GetCount(Join, Thread, RowReturnType.NotPassedUp),
                      marks.GetCount(Join, Thread, RowReturnType.End)));
    }

    [Fact]
    public void Rows_Read_By_A_Parent_That_Returns_Later_Are_Not_Passed_Up()
    {
        var marks = Build([(10, 100, RowStatus), (110, 120, RowStatus), (130, 140, EndStatus)],
                          [(12, 15, RowStatus), (20, 25, RowStatus), (30, 35, EndStatus)]);

        Assert.Equal((0, 2),
                     (marks.GetCount(Scan, Thread, RowReturnType.PassedUp), marks.GetCount(Scan, Thread, RowReturnType.NotPassedUp)));
    }

    [Fact]
    public void The_Nearest_Mark_Is_The_Call_That_Returned()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        var mark = marks.GetNearestMark(Scan, Thread, TraceTimelineAxis.Instructions, 24);

        Assert.Equal((1, RowReturnType.PassedUp), (mark?.Call, mark?.Type));
    }

    private static TraceRowReturnMarks Build((ulong Start, ulong End, ulong Status)[] joinCalls,
                                             (ulong Start, ulong End, ulong Status)[] scanCalls)
    {
        CallSpan[] spans = [.. joinCalls.Select((c, i) => Span(0, c.Start, c.End, (uint)i)),
                            .. scanCalls.Select((c, i) => Span(1, c.Start, c.End, (uint)i))];

        var builder = new TraceTimeline.Builder();

        builder.Add(spans);

        var timeline = builder.Build(Nodes);

        var tree = new CallStackTree();

        var join = tree.AddCall(tree.Root, Frame(JoinIterator), 1);

        var scan = tree.AddCall(join, Frame(ScanIterator), 1);

        CallStackNode[] nodes = [join, scan];

        timeline.MapNodes(nodes);

        var store = new TraceCallStore.Builder();

        store.Add(Helper, JoinIterator, Chunk([.. joinCalls.Select(c => Call(c, node: 0))]), joinCalls.Length);

        store.Add(Helper, ScanIterator, Chunk([.. scanCalls.Select(c => Call(c, node: 1))]), scanCalls.Length);

        var calls = store.Build();

        calls.MapNodes(nodes);

        timeline.SetCalls(calls);

        timeline.SetLifetimes([new TraceOperatorLifetime(Thread, Join, 0, [Whole()], [Whole()], MemoryInUseCurve.Empty),
                               new TraceOperatorLifetime(Thread, Scan, 1, [Whole()], [Whole()], MemoryInUseCurve.Empty)]);

        return TraceRowReturnMarks.Build(timeline, calls);
    }

    private static ulong[] Call((ulong Start, ulong End, ulong Status) call, int node)
        => Row(call.Start, flags: Thread | TraceCallColumns.ReturnedFlag, returnValue: call.Status, node: node, endSequence: call.End);

    private static CallstackFrame Frame(ulong instance) => new()
    {
        Module = "sqlmin",
        Rva = 0x10,
        Address = Helper,
        Instance = instance,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = "CQScanNew",
            MethodName = "GetRowOrReQualifyHelper",
            SymbolCategory = SymbolCategory.Unknown
        }
    };

    private static TraceSpan Whole() => new(0, 1000, 0, 0, TraceSpanFlags.None);

    private static CallSpan Span(int node, ulong start, ulong end, uint call)
        => new(start, 0, end, 0, start, end, node, Thread, call, 1);
}
