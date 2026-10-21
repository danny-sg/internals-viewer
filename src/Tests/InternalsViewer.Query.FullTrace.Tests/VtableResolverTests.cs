using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class VtableResolverTests
{
    [Fact]
    public void A_Vtable_Names_Its_Class()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CQScanTableScanNew::`vftable'");

        Assert.Equal("CQScanTableScanNew", collector.Build()[0x100]);
    }

    [Fact]
    public void A_Vtable_For_A_Base_Names_The_Derived_Class()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CMsqlReadOnlyXact::`vftable'{for `BaseXact'}");

        Assert.Equal("CMsqlReadOnlyXact", collector.Build()[0x100]);
    }

    [Fact]
    public void Other_Data_Is_Not_A_Vtable()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CQScanTableScanNew::s_Instance");

        Assert.Empty(collector.Build());
    }

    [Fact]
    public void A_Vtable_Shared_By_Two_Classes_Names_Neither()
    {
        var collector = new VtableResolver.Collector();

        collector.VisitData(0x100, "CQScanTableScanNew::`vftable'");

        collector.VisitData(0x100, "CQScanRangeNew::`vftable'");

        Assert.Empty(collector.Build());
    }
}
