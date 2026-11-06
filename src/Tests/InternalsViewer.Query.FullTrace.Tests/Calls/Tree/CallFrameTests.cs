using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Tests.Calls.Tree;

[Trait("Category", "Unit")]
public class CallFrameTests
{
    [Fact]
    public void A_Frame_Is_Qualified_With_Its_Module_And_Drops_The_Offset()
    {
        Assert.Equal("sqlmin!CQScanTableScanNew::GetRow", Resolved("CQScanTableScanNew::GetRow+0x1a3").QualifiedName);
    }

    [Fact]
    public void A_Template_Name_Is_Left_Unquoted()
    {
        Assert.Equal("sqlmin!CQScanTop<1>::GetRow", Resolved("CQScanTop<1>::GetRow+0x10").QualifiedName);
    }

    [Fact]
    public void An_Unresolved_Frame_Is_Its_Module_Relative_Address()
    {
        Assert.Equal("sqlmin+0x2509D40", new CallFrame { Module = new("sqlmin"), Rva = 0x2509D40 }.QualifiedName);
        Assert.Equal("sqlmin+0x2509D40", Resolved("0x2509D40", 0x2509D40).QualifiedName);
    }

    private static CallFrame Resolved(string symbol, uint rva = 0x1000)
    {
        SymbolsHelper.SplitOffset(symbol, out var name, out var offset);

        return new CallFrame { Module = new("sqlmin"), Rva = rva, Function = FrameFunction.Parse("sqlmin", name), Offset = offset };
    }
}
