using InternalsViewer.Mcp.Documents;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;

namespace InternalsViewer.Mcp.Tests.Documents;

[Trait("Category", "Unit")]
public class QueryCropTests
{
    private static readonly TracePosition Start = new(10, 0);

    private static readonly TracePosition End = new(20, 0);

    [Fact]
    public void A_Call_Under_A_Crop_Root_Is_Visible_And_One_Beside_It_Is_Hidden()
    {
        var tree = new CallStackTree();

        var execute = tree.AddCall(tree.Root, new CallstackFrame { Module = new("sqllang"), Rva = 0x10 }, 1);

        var scan = tree.AddCall(execute, new CallstackFrame { Module = new("sqlmin"), Rva = 0x20 }, 1);

        var compile = tree.AddCall(tree.Root, new CallstackFrame { Module = new("sqllang"), Rva = 0x30 }, 1);

        var roots = new HashSet<CallStackNode>([execute], ReferenceEqualityComparer.Instance);

        Assert.False(QueryCrop.IsHidden(execute, roots));
        Assert.False(QueryCrop.IsHidden(scan, roots));
        Assert.True(QueryCrop.IsHidden(compile, roots));
    }

    [Theory]
    [InlineData(5ul, true)]
    [InlineData(10ul, false)]
    [InlineData(20ul, false)]
    [InlineData(21ul, true)]
    public void A_Position_Outside_The_Query_Window_Is_Hidden(ulong sequence, bool expected)
        => Assert.Equal(expected, QueryCrop.IsHidden(new TracePosition(sequence, 0), Start, End));

    [Fact]
    public void The_Phase_Says_Whether_It_Ran_Before_Or_After_The_Query()
    {
        Assert.Contains("during compilation and setup", QueryCrop.GetPhase(new TracePosition(5, 0), Start, End));
        Assert.Contains("after the query has finished executing", QueryCrop.GetPhase(new TracePosition(25, 0), Start, End));
        Assert.Contains("in compilation, setup or teardown", QueryCrop.GetPhase(null, Start, End));
    }
}
