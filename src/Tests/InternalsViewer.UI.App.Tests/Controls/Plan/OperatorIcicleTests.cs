using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Plan;

namespace InternalsViewer.UI.App.Tests.Controls.Plan;

public class OperatorIcicleTests
{
    private static int _sequence;

    [Fact]
    public void Collapses_A_Straight_Chain_To_A_Single_Row()
    {
        var id = new PlanNodeIdentifier(1, 10);

        var tree = new CallStackTree();

        var engineEvent = Event(id);

        tree.Add([Frame("Y", 10), Frame("Seek", 20)], engineEvent);

        var collapsed = tree.CollapseToFunctions();

        var operatorEvent = Operator(id, NodeOf(collapsed, "Seek::m"));

        var hierarchy = OperatorHierarchy.Build([operatorEvent]);

        var segments = OperatorIcicle.Build(operatorEvent, hierarchy, [engineEvent], width: 120, height: 24, maxLevels: 4);

        var segment = Assert.Single(segments);

        Assert.Equal("Seek::m → Y::m", segment.Symbol);
        Assert.Equal(0, segment.X);
        Assert.Equal(0, segment.Y);
        Assert.Equal(120, segment.Width);
        Assert.Equal(24, segment.Height);
    }

    [Fact]
    public void Splits_A_Branch_By_Its_Share_Of_The_Events()
    {
        var id = new PlanNodeIdentifier(1, 10);

        var tree = new CallStackTree();

        var first = Event(id);
        var second = Event(id);
        var third = Event(id);

        tree.Add([Frame("A", 10), Frame("Seek", 20)], first);
        tree.Add([Frame("A", 10), Frame("Seek", 20)], second);
        tree.Add([Frame("B", 11), Frame("Seek", 20)], third);

        var collapsed = tree.CollapseToFunctions();

        var operatorEvent = Operator(id, NodeOf(collapsed, "Seek::m"));

        var hierarchy = OperatorHierarchy.Build([operatorEvent]);

        var segments = OperatorIcicle.Build(operatorEvent, hierarchy, [first, second, third],
                                            width: 120, height: 24, maxLevels: 4);

        Assert.Equal(3, segments.Count);

        var root = segments.Single(s => s.Symbol == "Seek::m");

        Assert.Equal(0, root.X);
        Assert.Equal(120, root.Width);
        Assert.Equal(12, root.Height);

        var left = segments.Single(s => s.Symbol == "A::m");

        Assert.Equal(0, left.X);
        Assert.Equal(12, left.Y);
        Assert.Equal(80, left.Width);

        var right = segments.Single(s => s.Symbol == "B::m");

        Assert.Equal(80, right.X);
        Assert.Equal(40, right.Width);
    }

    [Fact]
    public void Builds_Nothing_When_No_Scoped_Event_Passed_Through_An_Entry_Frame()
    {
        var scopedId = new PlanNodeIdentifier(1, 10);
        var unscopedId = new PlanNodeIdentifier(1, 99);

        var tree = new CallStackTree();

        var seekEvent = Event(unscopedId);
        var strayEvent = Event(scopedId);

        tree.Add([Frame("Y", 10), Frame("Seek", 20)], seekEvent);
        tree.Add([Frame("Z", 30), Frame("Root", 40)], strayEvent);

        var collapsed = tree.CollapseToFunctions();

        var operatorEvent = Operator(scopedId, NodeOf(collapsed, "Seek::m"));

        var hierarchy = OperatorHierarchy.Build([operatorEvent]);

        var segments = OperatorIcicle.Build(operatorEvent, hierarchy, [seekEvent, strayEvent],
                                            width: 120, height: 24, maxLevels: 4);

        Assert.Empty(segments);
    }

    [Fact]
    public void Stops_The_Segment_At_An_Exit_Frame()
    {
        var id = new PlanNodeIdentifier(1, 10);

        var tree = new CallStackTree();

        var engineEvent = Event(id);

        tree.Add([Frame("Y", 10), Frame("Child", 20), Frame("Seek", 30)], engineEvent);

        var collapsed = tree.CollapseToFunctions();

        var operatorEvent = Operator(id, NodeOf(collapsed, "Seek::m"));

        operatorEvent.ExitFrames = [NodeOf(collapsed, "Child::m")];

        var hierarchy = OperatorHierarchy.Build([operatorEvent]);

        var segments = OperatorIcicle.Build(operatorEvent, hierarchy, [engineEvent], width: 120, height: 24, maxLevels: 4);

        var segment = Assert.Single(segments);

        Assert.Equal("Seek::m → Child::m", segment.Symbol);
    }

    private static EngineEvent Event(PlanNodeIdentifier id)
        => new() { Name = "e", SequenceId = _sequence++, PlanNodeIdentifier = id };

    private static CallstackFrame Frame(string name, uint rva) => new()
    {
        Module = "sqllang",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = name, MethodName = "m" },
    };

    private static ExecutionOperatorEvent Operator(PlanNodeIdentifier id, params CallStackNode[] entryFrames) => new()
    {
        OperatorDescription = "op",
        PlanNodeIdentifier = id,
        EntryFrames = entryFrames,
    };

    private static CallStackNode NodeOf(CallStackTree tree, string symbol)
        => tree.Nodes().Single(node => node.Symbol == symbol);
}
