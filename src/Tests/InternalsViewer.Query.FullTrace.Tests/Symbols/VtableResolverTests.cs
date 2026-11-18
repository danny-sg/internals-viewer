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

        Assert.Equal(["CQScanTableScanNew"], collector.Build()[0x100]);
    }

    [Fact]
    public void A_Vtable_For_A_Base_Names_The_Derived_Class()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CMsqlReadOnlyXact::`vftable'{for `BaseXact'}");

        Assert.Equal(["CMsqlReadOnlyXact"], collector.Build()[0x100]);
    }

    [Theory]
    [InlineData("??_7RowsetNewSS@@6BIRowset@@@", "IRowset")]
    [InlineData("??_7RowsetNewSS@@6BIRowsetFetchNextRow@@@", "IRowsetFetchNextRow")]
    [InlineData("??_7CDerived@@6BIInner@Outer@@@", "Outer::IInner")]
    public void A_Vtable_For_A_Base_Names_The_Base_In_Its_Decorated_Name(string decorated, string expected)
        => Assert.Equal(expected, VtableResolver.GetBaseName(decorated));

    [Theory]
    [InlineData("??_7CQScanHash@@6B@")]
    [InlineData("??_7CDerived@@6B?$CTBase@H@@@")]
    [InlineData("?GetRow@CQScanHash@@QEAAEXZ")]
    public void A_Vtable_Without_A_Plain_Base_Or_Another_Symbol_Names_No_Base(string decorated)
        => Assert.Null(VtableResolver.GetBaseName(decorated));

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

        Assert.Equal(["RowsetWorkTableWindow", "RowsetWorkTableSS"], collector.Build()[0x100]);
    }

    [Fact]
    public void Only_A_Vtable_With_One_Class_Names_An_Object()
    {
        var vtables = new Dictionary<ulong, string[]>
        {
            [0x100] = ["CQScanTableScanNew"],
            [0x200] = ["RowsetWorkTableWindow", "RowsetWorkTableSS"]
        };

        Assert.Equal([(0x100ul, "CQScanTableScanNew")], VtableResolver.GetUniqueClasses(vtables).Select(c => (c.Key, c.Value)));
    }
}
