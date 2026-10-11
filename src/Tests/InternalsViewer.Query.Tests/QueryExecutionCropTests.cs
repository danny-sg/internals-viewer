using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.TimeTravel;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class QueryExecutionCropTests
{
    [Fact]
    public void The_Tree_Starts_At_The_Query_Execution_And_Drops_Compilation()
    {
        var tree = new CallStackTree();

        var source = tree.AddCall(tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1), Frame("CSQLSource", "Execute", 0x20), 1);

        tree.AddCall(source, Frame("CSQLSource", "Transform", 0x30), 1);

        var execute = tree.AddCall(source, Frame("CMsqlExecContext", "FExecute", 0x40), 1);

        var getRow = tree.AddCall(execute, Frame("CQScanTableScanNew", "GetRow", 0x50), 5);

        var crop = QueryExecutionCrop.Crop(tree);

        Assert.NotNull(crop);

        var root = Assert.Single(crop.Value.Tree.Root.ChildNodes);

        Assert.Equal("CMsqlExecContext::FExecute", root.Symbol);
        Assert.Equal(["CQScanTableScanNew::GetRow"], root.ChildNodes.Select(n => n.Symbol));
        Assert.Same(root, crop.Value.Projected[execute]);
        Assert.Equal(5, crop.Value.Projected[getRow].Calls);
        Assert.False(crop.Value.Projected.ContainsKey(source));
    }

    [Fact]
    public void A_Thread_That_Never_Executes_The_Query_Keeps_Its_Own_Root()
    {
        var tree = new CallStackTree();

        tree.AddCall(tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1), Frame("CMsqlExecContext", "FExecute", 0x40), 1);

        var worker = tree.AddCall(tree.Root, Frame("SubprocEntrypoint", "m", 0x60), 1);

        tree.AddCall(worker, Frame("CQScanXProducerNew", "GetRow", 0x70), 3);

        var crop = QueryExecutionCrop.Crop(tree);

        Assert.NotNull(crop);
        Assert.Equal(["CMsqlExecContext::FExecute", "SubprocEntrypoint::m"],
                     crop.Value.Tree.Root.ChildNodes.Select(n => n.Symbol).Order());
    }

    [Fact]
    public void A_Tree_Without_The_Query_Execution_Is_Not_Cropped()
    {
        var tree = new CallStackTree();

        tree.AddCall(tree.Root, Frame("ThreadEntry", "m", 0x10), 1);

        Assert.Null(QueryExecutionCrop.Crop(tree));
    }

    private static CallstackFrame Frame(string className, string methodName, uint rva) => new()
    {
        Module = "sqllang",
        Rva = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = className, MethodName = methodName }
    };
}
