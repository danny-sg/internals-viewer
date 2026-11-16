using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Tabs;

[Trait("Category", "Unit")]
public class MemoryTabTableTests
{
    [Fact]
    public void Every_Tab_Names_A_Type_In_Every_Layout()
        => Assert.All(EngineLayout.All.SelectMany(l => MemoryTabTable.Default.Tabs.Select(t => (Layout: l, Tab: t))),
                      p => Assert.True(p.Layout.IsA(p.Tab.Type, p.Tab.Type), $"{p.Layout.Name} has no {p.Tab.Type}"));

    [Fact]
    public void A_Type_Uses_The_Tab_Of_Its_Base_Class()
    {
        var table = new MemoryTabTable([new MemoryTab("Iterator", "CQScanNew", MemoryTabLayout.Column)]);

        Assert.Equal("Iterator", table.Find(EngineLayout.Latest, "CQScanHash")?.Header);
    }

    [Fact]
    public void A_Type_Without_A_Tab_Has_None()
        => Assert.Null(MemoryTabTable.Default.Find(EngineLayout.Latest, "CValHash"));

    [Fact]
    public void Every_Iterator_Uses_The_Iterator_Tab()
        => Assert.Equal("Iterator", MemoryTabTable.Default.Find(EngineLayout.Latest, "CQScanHash")?.Header);

    [Fact]
    public void A_Tab_Line_Is_Read_As_Header_Type_Layout_And_Trailing()
    {
        var reader = new StringReader("Header|Type|Layout|Trailing\nColumn Bindings|ColumnBinding|Row|ColumnBindingInfrequent");

        var tab = Assert.Single(MemoryTabTable.Read(reader).Tabs);

        Assert.Equal(new MemoryTab("Column Bindings", "ColumnBinding", MemoryTabLayout.Row, "ColumnBindingInfrequent"), tab);
    }

    [Fact]
    public void A_Tab_Line_With_An_Unknown_Layout_Is_Refused()
        => Assert.Throws<InvalidDataException>(() => MemoryTabTable.Read(new StringReader("Buffer|BUF|Diagonal|")));
}
