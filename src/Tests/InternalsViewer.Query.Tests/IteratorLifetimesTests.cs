using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel.Iterators;
using InternalsViewer.Query.CallStack.TimeTravel.Native;
using InternalsViewer.Query.CallStack.TimeTravel.Timeline;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class IteratorLifetimesTests
{
    private const ulong Hash = 0x100;

    private const ulong Scan = 0x200;

    [Fact]
    public void An_Operator_Lives_From_Its_First_Call_To_Its_Last()
    {
        var tree = new CallStackTree();

        var caller = tree.AddCall(tree.Root, Frame(0x10, 0), 1);
        var open = tree.AddCall(caller, Frame(0x20, Hash), 1);
        var getRow = tree.AddCall(caller, Frame(0x30, Hash), 2);
        var helper = tree.AddCall(getRow, Frame(0x40, Hash), 1);
        var close = tree.AddCall(caller, Frame(0x50, Hash), 1);

        var builder = new TimeTravelTimeline.Builder();

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

        var lifetime = Assert.Single(IteratorLifetimes.Build(timeline, [hashMatch]));

        var calls = lifetime.Calls(TimeTravelTimelineAxis.Instructions);

        var axis = TimeTravelTimelineAxis.Instructions;

        Assert.Same(hashMatch, lifetime.Operator);
        Assert.Equal((0d, 45d, 4), (lifetime.StartOf(axis), lifetime.EndOf(axis), calls.Count));
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

        var builder = new TimeTravelTimeline.Builder();

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

        timeline.SetMemory([new(7, 5, 6, 100, 0xA), new(7, 22, 23, 50, 0xB)], [new(7, 41, 42, 0xA)]);

        var hashMatch = Operator("Hash Match", 1, Hash);

        var scan = Operator("Index Scan", 2, Scan);

        var lifetimes = IteratorLifetimes.Build(timeline, [hashMatch, scan]);

        var hashLifetime = lifetimes.Single(l => ReferenceEquals(l.Operator, hashMatch));

        var scanLifetime = lifetimes.Single(l => ReferenceEquals(l.Operator, scan));

        Assert.Equal((100ul, 0ul, 50ul),
                     (hashLifetime.InUse.PeakDuring(0, 40), hashLifetime.InUse.PeakDuring(42, 45), scanLifetime.InUse.PeakDuring(21, 24)));
    }

    private static ExecutionOperatorEvent Operator(string name, int nodeId, ulong instance) => new()
    {
        Name = name,
        OperatorDescription = name,
        PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = nodeId },
        Instances = [instance]
    };

    private static CallstackFrame Frame(ulong address, ulong instance)
        => new() { Module = "sqlmin", Rva = (uint)address, Address = address, Instance = instance };

    private static TimeTravelCallSpan Span(int node, ulong start, ulong end)
        => new(start, 0, end, 0, start, end, node, 7, uint.MaxValue, 1);
}
