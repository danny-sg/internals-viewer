using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests.Iterators;

[Trait("Category", "Unit")]
public class IteratorLayoutTests
{
    private const ulong Top = 0x1F4_0000_1000;

    private const ulong Inputs = 0x1F4_0000_2000;


    [Fact]
    public void A_Scan_Marks_Its_Row_Buffer_And_Bindings()
    {
        var fields = IteratorLayout.GetFields("CQScanTableScanNew", IteratorTargetType.Iterator, new byte[0x170], Targets());

        Assert.Equal([("Inputs", 0x08),
                      ("Execution Context", 0x18),
                      ("Rowset", 0x48),
                      ("Prefetch Manager", 0x70),
                      ("Row Buffer", 0x80),
                      ("Row Bindings", 0x88),
                      ("Client Row", 0x168)],
                     fields.Select(f => (f.Name, f.Offset)));
    }

    [Fact]
    public void A_Profiling_Iterator_Has_The_Iterator_Header_And_Names_The_Iterator_It_Wraps()
    {
        var data = new byte[0xA0];

        BitConverter.GetBytes(Top).CopyTo(data, 0x90);

        var fields = IteratorLayout.GetFields("CQScanProfileNew", IteratorTargetType.ProfilingIterator, data, Targets());

        Assert.Equal([("Inputs", 0x08), ("Execution Context", 0x18), ("Iterator › Top (Node 0)", 0x90)],
                     fields.Select(f => (f.Name, f.Offset)));
    }

    [Fact]
    public void A_Compiled_Top_Marks_Its_Header_And_Top_Count_Expression()
    {
        var fields = IteratorLayout.GetFields("CXteTop", IteratorTargetType.PlanObject, new byte[0xB0], Targets());

        Assert.Equal([("Node Id", 0x0C), ("Inputs", 0x18), ("Query", 0x28), ("Output Columns", 0x30), ("Top Count Expression", 0x88)],
                     fields.Select(f => (f.Name, f.Offset)));
    }

    [Fact]
    public void Inputs_Are_Read_Until_An_Entry_Is_Not_An_Iterator()
    {
        var memory = new FakeTraceReplay().Write(Inputs, Top).Write(Inputs + 0x08, 0x1234UL);

        Assert.Equal([Top], IteratorLayout.GetInputs(memory, Inputs, Targets()));
    }

    private static Dictionary<ulong, IteratorTarget> Targets()
    {
        var tree = new CallTree();

        var getRow = tree.AddCall(tree.Root, new CallFrame { Module = new("sqlmin"), Rva = 0x10, InstanceAddress = Top }, 1);

        var top = new ExecutionOperatorEvent
        {
            Name = "Top",
            OperatorDescription = "Top",
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 1, NodeId = 0 }
        };

        return new Dictionary<ulong, IteratorTarget> { [Top] = new(getRow, top) };
    }
}
