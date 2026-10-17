using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Output.Enums;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Parsers;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceRowReturnMarksTests
{
    private const uint Thread = 7;

    private const ulong Helper = 0x1000;

    private const ulong JoinIterator = 0x1F4_0000_1000;

    private const ulong ScanIterator = 0x1F4_0000_2000;

    private const ulong ScanBuffer = 0x1F4_0000_3000;

    private const ulong OutputRow = 0x1F4_0000_9000;

    private const ulong RowStatus = 0;

    private const ulong EndStatus = 0x40EC6;

    private static readonly ExecutionOperatorEvent Join = new()
    {
        Name = "Merge Join",
        OperatorDescription = "Merge Join",
        Instances = [JoinIterator],
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 0)
    };

    private static readonly ExecutionOperatorEvent Scan = new()
    {
        Name = "Index Scan",
        OperatorDescription = "Index Scan",
        Instances = [ScanIterator],
        PlanNodeIdentifier = new PlanNodeIdentifier(1, 1),
        ParentNodeId = 0
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

    [Fact]
    public void A_Point_Inside_A_GetRow_Call_Finds_That_Call()
    {
        var marks = Build([(10, 40, RowStatus), (50, 70, EndStatus)], [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]);

        var call = marks.GetCall(Scan, Thread, TraceTimelineAxis.Instructions, 22);

        Assert.Equal((1, 20d, 25d), (call?.Call, call?.Start, call?.End));
        Assert.Null(marks.GetCall(Scan, Thread, TraceTimelineAxis.Instructions, 17));
    }

    [Fact]
    public void Each_Row_The_Root_Operator_Returns_Is_An_Output_Row()
    {
        var outputRows = Build([(10, 40, RowStatus), (50, 80, RowStatus), (90, 100, EndStatus)],
                               [(12, 15, RowStatus), (55, 60, RowStatus), (92, 95, EndStatus)]).OutputRows;

        Assert.Equal([(1, 0), (2, 1)], outputRows.All.Select(r => (r.Ordinal, r.Return.Call)));
    }

    [Fact]
    public void An_Output_Row_Comes_From_The_Last_Row_Buffer_Its_Source_Captured_Before_It_Was_Sent()
    {
        var outputRow = Assert.Single(Build([(10, 40, RowStatus), (50, 70, EndStatus)],
                                            [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)],
                                            CreateLineage).OutputRows.All);

        var source = Assert.Single(outputRow.Sources);

        Assert.Equal((Scan, 1, 25d), (source.Operator, source.RowBuffer?.Call, source.RowBuffer?.Position));
        Assert.Equal([0], source.Columns);
    }

    [Fact]
    public void The_GetRow_Path_Links_Each_Row_Passed_Up_To_The_Return_Of_Its_Parent_Operator()
    {
        var outputRow = Assert.Single(Build([(10, 40, RowStatus), (50, 70, EndStatus)],
                                            [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]).OutputRows.All);

        var step = Assert.Single(outputRow.Steps);

        Assert.Equal((Scan, 1, Join, 0), (step.Return.Operator, step.Return.Call, step.Parent.Operator, step.Parent.Call));
    }

    [Fact]
    public void Each_Output_Row_Takes_The_Last_Row_Buffer_Its_Source_Captured_Before_The_Root_Returned()
    {
        var outputRows = Build([(10, 40, RowStatus), (50, 80, RowStatus), (90, 100, EndStatus)],
                               [(12, 15, RowStatus), (55, 60, RowStatus), (92, 95, EndStatus)],
                               t => CreateLineage(t, ScanColumns(), 15, 60)).OutputRows;

        Assert.Equal([0, 1], outputRows.All.Select(r => Assert.Single(r.Sources).RowBuffer?.Call));
    }

    [Fact]
    public void A_Row_Sent_Directly_Takes_Every_Column_From_The_Row_Buffer_Of_Its_Sender()
    {
        var structure = new TraceOutputStructure(OutputRow, [], ScanIterator);

        var outputRow = Assert.Single(Build([(10, 40, RowStatus), (50, 70, EndStatus)],
                                            [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)],
                                            t => CreateLineage(t, structure, 15, 25)).OutputRows.All);

        var source = Assert.Single(outputRow.Sources);

        Assert.Equal((Scan, 1), (source.Operator, source.RowBuffer?.Call));
        Assert.Equal([0, 1], source.Columns);
    }

    [Fact]
    public void The_Output_Row_At_A_Point_Is_The_One_Whose_Root_Call_Contains_It()
    {
        var outputRows = Build([(10, 40, RowStatus), (50, 80, RowStatus), (90, 100, EndStatus)],
                               [(12, 15, RowStatus), (55, 60, RowStatus), (92, 95, EndStatus)]).OutputRows;

        Assert.Equal([1, null, 2],
                     new[] { 30d, 45d, 60d }.Select(p => outputRows.GetRow(TraceTimelineAxis.Instructions, p)?.Ordinal));
    }

    [Fact]
    public void An_Output_Row_Without_Captured_Values_Has_No_Sources()
    {
        var outputRow = Assert.Single(Build([(10, 40, RowStatus), (50, 70, EndStatus)],
                                            [(12, 15, RowStatus), (20, 25, RowStatus), (55, 60, EndStatus)]).OutputRows.All);

        Assert.Empty(outputRow.Sources);
    }

    [Fact]
    public void The_Nearest_Output_Row_Is_The_Root_Return_Closest_To_The_Point()
    {
        var outputRows = Build([(10, 40, RowStatus), (50, 80, RowStatus), (90, 100, EndStatus)],
                               [(12, 15, RowStatus), (55, 60, RowStatus), (92, 95, EndStatus)]).OutputRows;

        Assert.Equal(2, outputRows.GetNearest(TraceTimelineAxis.Instructions, 75)?.Ordinal);
    }

    private static TraceRowReturnMarks Build((ulong Start, ulong End, ulong Status)[] joinCalls,
                                             (ulong Start, ulong End, ulong Status)[] scanCalls,
                                             Func<TraceTimeline, TraceOutputLineage>? createLineage = null)
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

        return TraceRowReturnMarks.Build(timeline, calls, createLineage?.Invoke(timeline));
    }

    private static TraceOutputLineage CreateLineage(TraceTimeline timeline) => CreateLineage(timeline, ScanColumns(), 15, 25);

    private static TraceOutputLineage CreateLineage(TraceTimeline timeline, TraceOutputStructure structure, params ulong[] captured)
    {
        RowBufferColumn[] columns = [ColumnValueParser.Create(0, RowBufferBinding.None),
                                     ColumnValueParser.Create(1, RowBufferBinding.None)];

        var rowBuffers = new RowBufferStore([.. captured.Select((p, i) => new RowBuffer(Helper,
                                                                                         ScanIterator,
                                                                                         i,
                                                                                         At(p),
                                                                                         ScanIterator,
                                                                                         ScanBuffer,
                                                                                         columns))]);

        return TraceOutputLineage.Build(timeline, [structure], rowBuffers);
    }

    private static TraceOutputStructure ScanColumns()
        => new(OutputRow, [new TraceOutputSource(OutputSourceType.Buffer, 0, ScanBuffer) { Owner = ScanIterator }], 0);

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
