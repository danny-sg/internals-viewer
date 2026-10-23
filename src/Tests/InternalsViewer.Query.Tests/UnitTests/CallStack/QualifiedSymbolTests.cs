using InternalsViewer.Query.CallStack;

namespace InternalsViewer.Query.Tests.UnitTests.CallStack;

[Trait("Category", "Unit")]
public class QualifiedSymbolTests
{
    [Fact]
    public void A_Frame_Is_Qualified_With_Its_Module_And_Drops_The_Offset()
    {
        Assert.Equal("sqlmin!CQScanTableScanNew::GetRow", QualifiedSymbol.Of(Resolved("CQScanTableScanNew::GetRow+0x1a3")));
    }

    [Fact]
    public void A_Template_Name_Is_Left_Unquoted()
    {
        Assert.Equal("sqlmin!CQScanTop<1>::GetRow", QualifiedSymbol.Of(Resolved("CQScanTop<1>::GetRow+0x10")));
    }

    [Fact]
    public void An_Unresolved_Frame_Is_Its_Module_Relative_Address()
    {
        Assert.Equal("sqlmin+0x2509D40", QualifiedSymbol.Of(new CallstackFrame { Module = "sqlmin", Rva = 0x2509D40 }));
        Assert.Equal("sqlmin+0x2509D40", QualifiedSymbol.Of(Resolved("0x2509D40", 0x2509D40)));
    }

    [Fact]
    public void A_Member_Is_Qualified_With_Its_Module_And_Class()
    {
        var member = new ClassMemberReference(new ClassMember("sqlmin", "AcquireHoBtRowGroupLock", "(unsigned long)", 0x1000, true),
                                              "HoBtAccess",
                                              false);

        Assert.Equal("sqlmin!HoBtAccess::AcquireHoBtRowGroupLock", QualifiedSymbol.Of(member));
    }

    [Fact]
    public void A_Member_Without_A_Class_Is_Qualified_With_Its_Module()
    {
        var member = new ClassMemberReference(new ClassMember("sqlmin", "memcpy", "(void *,void *,size_t)", 0x1000, true), "", false);

        Assert.Equal("sqlmin!memcpy", QualifiedSymbol.Of(member));
    }

    private static CallstackFrame Resolved(string symbol, uint rva = 0x1000) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Resolved = ResolvedCallstackFrameParser.Parse("sqlmin", symbol)
    };
}
