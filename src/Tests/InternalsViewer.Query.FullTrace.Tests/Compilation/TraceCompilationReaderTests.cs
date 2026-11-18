using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Compilation;
using InternalsViewer.Query.FullTrace.Timeline;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Compilation;

[Trait("Category", "Unit")]
public class TraceCompilationReaderTests
{
    private const uint Thread = 7;

    [Theory]
    [InlineData("SqlSemantics::DoSemantics__query_specification__action1", "Query Specification", "Action 1")]
    [InlineData("SqlSemantics::DoSemantics__query_specification__all_distinct_topn", "Query Specification", "All Distinct Topn")]
    [InlineData("SqlSemantics::DoSemantics__statements__first", "Statements", "First")]
    [InlineData("CRelOp_Get::BindTree", "CRelOp_Get", "BindTree")]
    [InlineData("CScaOp_Identifier::BindSelf", "CScaOp_Identifier", "BindSelf")]
    public void A_Grammar_Action_Or_Bind_Call_Is_A_Step(string symbol, string name, string detail)
        => Assert.Equal<(string, string?)>((name, detail), TraceCompilationReader.GetStepName(symbol)!.Value);

    [Theory]
    [InlineData("COptExpr::BindTree")]
    [InlineData("CScaOpArg::BindTree")]
    [InlineData("CLexer::LGetToken")]
    [InlineData("GenTable")]
    public void A_Wrapper_Or_Other_Call_Is_Not_A_Step(string symbol)
        => Assert.Null(TraceCompilationReader.GetStepName(symbol));

    [Fact]
    public void Steps_Nest_By_The_Calls_They_Run_Inside()
    {
        var roots = TraceCompilationReader.Nest([Step("Bind", 20, 30), Step("Algebrization", 10, 40), Step("Parse", 1, 5)]);

        Assert.Equal(["Parse", "Algebrization"], roots.Select(r => r.Name));

        Assert.Equal(["Bind"], roots[1].Steps.Select(s => s.Name));
    }

    [Fact]
    public void An_Object_Goes_To_The_Innermost_Step_That_Constructed_It_Once()
    {
        var roots = TraceCompilationReader.Nest([Step("Algebrization", 10, 40), Step("Bind", 20, 30)]);

        TraceCompilationReader.Place(roots,
                                     [Constructed(15, "CAlgStmt", 0xA0),
                                      Constructed(22, "CValRefAlgTable", 0xB0),
                                      Constructed(23, "CValRefAlgTableBase", 0xB0),
                                      Constructed(50, "CXteTop", 0xC0)]);

        Assert.Equal(["CAlgStmt"], roots[0].Objects.Select(o => o.ClassName));

        Assert.Equal(["CValRefAlgTable"], roots[0].Steps[0].Objects.Select(o => o.ClassName));
    }

    private static TraceCompileStep Step(string name, ulong from, ulong to)
        => new(name, null, new CallTreeNode(), Thread, new TraceSpan(new TracePoint(At(from), 0), new TracePoint(At(to), 0), 0, 0, default));

    private static (uint, TracePosition, TraceCompileObject) Constructed(ulong position, string className, ulong address)
        => (Thread, At(position), new TraceCompileObject(className, address, 0, At(position)));
}
