using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class IteratorLifetimeBuilderTests
{
    private const ulong Hash = 0x100;

    private const ulong Scan = 0x200;

    private const ulong BatchHelper = 0x300;

    private const ulong BatchFilter = 0x400;

    private const ulong Query = 0x1F4_0000_1000;

    private const uint Thread = 7;

    [Fact]
    public void An_Operator_Lives_From_Its_First_Call_To_Its_Last()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, 0), 1);
        var open = tree.AddCall(caller, Frame(0x20, Hash), 1);
        var getRow = tree.AddCall(caller, Frame(0x30, Hash), 2);
        var helper = tree.AddCall(getRow, Frame(0x40, Hash), 1);
        var close = tree.AddCall(caller, Frame(0x50, Hash), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100),
                     Span(1, 0, 10),
                     Span(2, 20, 25),
                     Span(3, 21, 24),
                     Span(2, 30, 35),
                     Span(4, 40, 45)]);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1),
                                      new(0, 0, 0x20, Hash, 1),
                                      new(0, 0, 0x30, Hash, 2),
                                      new(2, 0, 0x40, Hash, 1),
                                      new(0, 0, 0x50, Hash, 1)]);

        timeline.MapNodes([caller, open, getRow, helper, close]);

        var hashMatch = new ExecutionOperatorEvent
        {
            Name = "Hash Match",
            OperatorDescription = "Hash Match",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 1 },
            Instances = [Hash]
        };

        var lifetime = Assert.Single(IteratorLifetimeBuilder.Build(timeline, MemoryIndex.Empty, [hashMatch], [], []));

        var axis = TraceTimelineAxis.Instructions;

        Assert.Same(hashMatch, lifetime.Operator);
        Assert.Equal((0d, 45d, 4), (lifetime.GetStart(axis), lifetime.GetEnd(axis), lifetime.Calls.Count));
    }

    [Fact]
    public void Memory_Allocated_Inside_A_Child_Operator_Is_In_Use_On_The_Child()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, 0), 1);
        var open = tree.AddCall(caller, Frame(0x20, Hash), 1);
        var getRow = tree.AddCall(caller, Frame(0x30, Hash), 1);
        var scanRow = tree.AddCall(getRow, Frame(0x60, Scan), 1);
        var close = tree.AddCall(caller, Frame(0x50, Hash), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100),
                     Span(1, 0, 10),
                     Span(2, 20, 30),
                     Span(3, 21, 24),
                     Span(4, 40, 45)]);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1),
                                      new(0, 0, 0x20, Hash, 1),
                                      new(0, 0, 0x30, Hash, 1),
                                      new(2, 0, 0x60, Scan, 1),
                                      new(0, 0, 0x50, Hash, 1)]);

        timeline.MapNodes([caller, open, getRow, scanRow, close]);

        var memory = MemoryIndex.Build([new(7, At(5), At(6), 100, 0xA), new(7, At(22), At(23), 50, 0xB)], [new(7, At(41), At(42), 0xA)]);

        var hashMatch = Operator("Hash Match", 1, Hash);

        var scan = Operator("Index Scan", 2, Scan);

        var lifetimes = IteratorLifetimeBuilder.Build(timeline, memory, [hashMatch, scan], [], []);

        var hashLifetime = lifetimes.Single(l => ReferenceEquals(l.Operator, hashMatch));

        var scanLifetime = lifetimes.Single(l => ReferenceEquals(l.Operator, scan));

        Assert.Equal((100ul, 0ul, 50ul),
                     (hashLifetime.InUse.PeakDuring(At(0), At(40)),
                      hashLifetime.InUse.PeakDuring(At(42), At(45)),
                      scanLifetime.InUse.PeakDuring(At(21), At(24))));
    }

    [Fact]
    public void A_Batch_Helper_Is_A_Batch_To_Row_Operator_For_The_Node_It_Reports()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, 0), 1);
        var helperRow = tree.AddCall(caller, Frame(0x70, BatchHelper), 1);
        var scanBatch = tree.AddCall(helperRow, Frame(0x60, Scan), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 10, 50), Span(2, 20, 30)]);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, 0x70, BatchHelper, 1), new(1, 0, 0x60, Scan, 1)]);

        timeline.MapNodes([caller, helperRow, scanBatch]);

        var scan = Operator("Columnstore Index Scan", 2, Scan) with { NodeLevel = 2 };

        IteratorPlanNode[] planNodes = [new(BatchHelper, 0xA0, 0, Query, 2, IsBatchHelper: true)];

        var lifetimes = IteratorLifetimeBuilder.Build(timeline, MemoryIndex.Empty, [scan], planNodes, []);

        var adapter = Assert.Single(lifetimes, l => l.Operator.Instances.Contains(BatchHelper)).Operator;

        Assert.Equal(("Batch To Row Adapter", OperatorCategory.Adapter, 2), (adapter.Name, adapter.Category, adapter.NodeLevel));
        Assert.Null(adapter.PlanNodeIdentifier);
    }

    [Fact]
    public void A_Batch_Helper_For_A_Node_The_Plan_Does_Not_Show_Adapts_The_Operator_Beneath_It()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, 0), 1);
        var helperRow = tree.AddCall(caller, Frame(0x70, BatchHelper), 1);
        var filterBatch = tree.AddCall(helperRow, Frame(0x80, BatchFilter), 1);
        var scanBatch = tree.AddCall(filterBatch, Frame(0x60, Scan), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 10, 50), Span(2, 15, 45), Span(3, 20, 30)]);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1),
                                      new(0, 0, 0x70, BatchHelper, 1),
                                      new(1, 0, 0x80, BatchFilter, 1),
                                      new(2, 0, 0x60, Scan, 1)]);

        timeline.MapNodes([caller, helperRow, filterBatch, scanBatch]);

        var scan = Operator("Columnstore Index Scan", 2, Scan) with { NodeLevel = 2 };

        IteratorPlanNode[] planNodes = [new(BatchHelper, 0xA0, 0, Query, 1, IsBatchHelper: true)];

        var lifetimes = IteratorLifetimeBuilder.Build(timeline, MemoryIndex.Empty, [scan], planNodes, []);

        var adapter = Assert.Single(lifetimes, l => l.Operator.Instances.Contains(BatchHelper)).Operator;

        Assert.Equal(("Batch To Row Adapter", 2), (adapter.Name, adapter.NodeLevel));
    }

    [Fact]
    public void An_Operator_Exists_From_Its_Construction_To_The_Next_Query_Teardown_On_Its_Thread()
    {
        var (timeline, hashMatch) = BuildHashTimeline();

        IteratorPlanNode[] planNodes = [new(Hash, 0xA0, 0, Query, 1, new TracePoint(At(5), 5))];

        QueryTeardown[] teardowns = [new(Thread, new TracePoint(At(15), 15)), new(Thread, new TracePoint(At(70), 70))];

        var lifetime = Assert.Single(IteratorLifetimeBuilder.Build(timeline, MemoryIndex.Empty, [hashMatch], planNodes, teardowns));

        Assert.Equal((5d, 70d, 5d, 70d, 20d, 45d),
                     (lifetime.GetConstructed(TraceTimelineAxis.Position),
                      lifetime.GetDestroyed(TraceTimelineAxis.Position),
                      lifetime.GetConstructed(TraceTimelineAxis.Instructions),
                      lifetime.GetDestroyed(TraceTimelineAxis.Instructions),
                      lifetime.GetStart(TraceTimelineAxis.Instructions),
                      lifetime.GetEnd(TraceTimelineAxis.Instructions)));
    }

    [Fact]
    public void An_Operator_Without_A_Recorded_Construction_Or_Teardown_Exists_For_The_Whole_Recording()
    {
        var (timeline, hashMatch) = BuildHashTimeline();

        var lifetime = Assert.Single(IteratorLifetimeBuilder.Build(timeline, MemoryIndex.Empty, [hashMatch], [], []));

        Assert.Equal((0d, 100d),
                     (lifetime.GetConstructed(TraceTimelineAxis.Instructions), lifetime.GetDestroyed(TraceTimelineAxis.Instructions)));
    }

    private static (TraceTimeline Timeline, ExecutionOperatorEvent Operator) BuildHashTimeline()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, 0), 1);
        var getRow = tree.AddCall(caller, Frame(0x30, Hash), 2);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100), Span(1, 20, 25), Span(1, 40, 45)]);

        var timeline = builder.Build([new(-1, 0, 0x10, 0, 1), new(0, 0, 0x30, Hash, 2)]);

        timeline.MapNodes([caller, getRow]);

        return (timeline, Operator("Hash Match", 1, Hash));
    }

    private static ExecutionOperatorEvent Operator(string name, int nodeId, ulong instance) => new()
    {
        Name = name,
        OperatorDescription = name,
        PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = nodeId },
        Instances = [instance]
    };

    private static CallstackFrame Frame(ulong address, ulong instance)
        => new() { Module = "sqlmin", Rva = (uint)address, Address = address, InstanceAddress = instance };

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end)
        => new(start, 0, end, 0, start, end, callNodeIndex, Thread, uint.MaxValue, 1);
}
