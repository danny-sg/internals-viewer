using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Tests.Symbols;

[Trait("Category", "Unit")]
public class VtableResolverTests
{
    [Fact]
    public void A_Vtable_Names_Its_Class()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CQScanTableScanNew::`vftable'");

        Assert.Equal([new VtableSymbol("CQScanTableScanNew", null)], collector.Build()[0x100]);
    }

    [Fact]
    public void A_Vtable_For_A_Base_Names_The_Derived_Class()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CMsqlReadOnlyXact::`vftable'{for `BaseXact'}");

        Assert.Equal([new VtableSymbol("CMsqlReadOnlyXact", "BaseXact")], collector.Build()[0x100]);
    }

    [Fact]
    public void The_Decorated_Name_Gives_The_Base_A_Vtable_Is_For()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CValRefJoin::`vftable'", "??_7CValRefJoin@@6BCValRef@@@");

        collector.VisitData(0x200, "CValRefJoin::`vftable'", "??_7CValRefJoin@@6BCValComp@@@");

        collector.VisitData(0x300, "CValHash::`vftable'", "??_7CValHash@@6B@");

        var vtables = collector.Build();

        Assert.Equal([new VtableSymbol("CValRefJoin", "CValRef"),
                      new VtableSymbol("CValRefJoin", "CValComp"),
                      new VtableSymbol("CValHash", null)],
                     [vtables[0x100][0], vtables[0x200][0], vtables[0x300][0]]);
    }

    [Fact]
    public void A_Base_In_A_Namespace_Is_Named_In_Full()
        => Assert.Equal("Ns::Base", VtableResolver.GetBaseName("Derived::`vftable'", "??_7Derived@@6BBase@Ns@@@"));

    [Fact]
    public void Other_Data_Is_Not_A_Vtable()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CQScanTableScanNew::s_Instance");

        collector.VisitData(0x200, "CQScanTableScanNew::`RTTI Complete Object Locator'");

        Assert.Empty(collector.Build());
    }

    [Fact]
    public void A_Shared_Vtable_Keeps_Every_Class_In_Symbol_Order()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "RowsetWorkTableWindow::`vftable'");

        collector.VisitData(0x100, "RowsetWorkTableSS::`vftable'");

        collector.VisitData(0x100, "RowsetWorkTableWindow::`vftable'");

        Assert.Equal(["RowsetWorkTableWindow", "RowsetWorkTableSS"], collector.Build()[0x100].Select(s => s.ClassName));
    }

    [Fact]
    public void Only_A_Vtable_With_One_Class_Names_An_Object()
    {
        var vtables = new Dictionary<ulong, VtableSymbol[]>
        {
            [0x100] = [new("CQScanTableScanNew", null)],
            [0x200] = [new("RowsetWorkTableWindow", null), new("RowsetWorkTableSS", null)]
        };

        Assert.Equal([(0x100ul, "CQScanTableScanNew")], VtableResolver.GetUniqueClasses(vtables).Select(c => (c.Key, c.Value)));
    }
}
