using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Output;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class TraceOutputLineageTests
{
    private const uint Thread = 7;

    private const ulong JoinIterator = 0x1F4_0000_1000;

    private const ulong ScanIterator = 0x1F4_0000_2000;

    private const ulong OutputRow = 0x1F4_0000_9000;

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

    [Fact]
    public void Each_Output_Column_Comes_From_The_Operator_That_Owns_It()
    {
        var source = Assert.Single(GetSources(ScanColumns()));

        Assert.Equal((Scan, false), (source.Operator, source.IsWholeRow));
        Assert.Equal([0, 1], source.Columns);
    }

    [Fact]
    public void A_Row_Sent_Directly_Comes_Whole_From_The_Iterator_That_Sent_It()
    {
        var source = Assert.Single(GetSources(new TraceOutputStructure(OutputRow, [], ScanIterator)));

        Assert.Equal((Scan, true), (source.Operator, source.IsWholeRow));
    }

    [Fact]
    public void Columns_Owned_By_An_Iterator_No_Operator_Runs_Have_No_Source()
        => Assert.Empty(GetSources(new TraceOutputStructure(OutputRow, [0x1F4_0000_A000], 0)));

    [Fact]
    public void Columns_Owned_By_A_Batch_To_Row_Operator_Come_From_That_Operator()
    {
        var adapter = new ExecutionOperatorEvent
        {
            Name = "Batch To Row Adapter",
            OperatorDescription = "Batch To Row Adapter",
            Instances = [ScanIterator]
        };

        var source = Assert.Single(GetSources(ScanColumns(), adapter));

        Assert.Same(adapter, source.Operator);
    }

    [Fact]
    public void Output_Rows_Without_A_Structure_Have_No_Sources()
        => Assert.Empty(TraceOutputLineage.GetSources(GetLifetimes(Scan), []));

    private static IReadOnlyList<TraceOutputRowSource> GetSources(TraceOutputStructure structure, ExecutionOperatorEvent? child = null)
        => TraceOutputLineage.GetSources(GetLifetimes(child ?? Scan), [structure]);

    private static TraceOperatorLifetimes GetLifetimes(ExecutionOperatorEvent child) => new([Lifetime(Join, 0), Lifetime(child, 1)]);

    private static TraceOperatorLifetime Lifetime(ExecutionOperatorEvent operatorEvent, int index)
        => new(Thread, operatorEvent, index, [Whole()], [Whole()], MemoryInUseCurve.Empty);

    private static TraceOutputStructure ScanColumns() => new(OutputRow, [ScanIterator, ScanIterator], 0);

    private static TraceSpan Whole() => new(0, 1000, 0, 0, TraceSpanFlags.None);
}
