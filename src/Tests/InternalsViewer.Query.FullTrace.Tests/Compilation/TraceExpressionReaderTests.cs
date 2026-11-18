using InternalsViewer.Query.FullTrace.Compilation;

namespace InternalsViewer.Query.FullTrace.Tests.Compilation;

[Trait("Category", "Unit")]
public class TraceExpressionReaderTests
{
    private static readonly Dictionary<ulong, string> Classes = new()
    {
        [0x1100] = "CRelOp_SelectQuery",
        [0x2100] = "CRelOp_QuerySpec",
        [0x3100] = "CRelOp_Get"
    };

    [Fact]
    public void An_Expression_Tree_Is_Read_From_Its_Operators_And_Child_Slots()
    {
        var memory = new FakeTraceReplay();

        Expression(memory, 0x1000, 0x1100, 0x1080, [0x2000, 0]);

        Expression(memory, 0x2000, 0x2100, 0x2080, [0, 0x3000, 0]);

        Expression(memory, 0x3000, 0x3100, 0, []);

        var root = TraceExpressionReader.Read(memory, a => Classes.GetValueOrDefault(a), 0x1000);

        Assert.Equal("CRelOp_SelectQuery", root?.ClassName);

        var spec = Assert.Single(root!.Children);

        Assert.Equal(("CRelOp_QuerySpec", "CRelOp_Get"), (spec.ClassName, Assert.Single(spec.Children).ClassName));
    }

    [Fact]
    public void A_Child_That_Points_Back_Up_The_Tree_Is_Not_Read_Again()
    {
        var memory = new FakeTraceReplay();

        Expression(memory, 0x1000, 0x1100, 0x1080, [0x2000]);

        Expression(memory, 0x2000, 0x2100, 0x2080, [0x1000]);

        var root = TraceExpressionReader.Read(memory, a => Classes.GetValueOrDefault(a), 0x1000);

        Assert.Empty(Assert.Single(root!.Children).Children);
    }

    private static void Expression(FakeTraceReplay memory, ulong address, ulong operatorAddress, ulong children, ulong[] slots)
    {
        var layout = memory.Layout.OptimizerExpression;

        memory.Write(address + (ulong)layout.OperatorField.Offset, operatorAddress);

        memory.Write(address + (ulong)layout.ChildCountField.Offset, slots.Length);

        memory.Write(address + (ulong)layout.ChildrenField.Offset, children);

        for (var index = 0; index < slots.Length; index++)
        {
            memory.Write(children + (ulong)(index * sizeof(ulong)), slots[index]);
        }
    }
}
