using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.Tests.Helpers;

[Trait("Category", "Unit")]
public class DataPathTextTests
{
    [Fact]
    public void An_Object_Is_Named_By_Schema_Table_And_Index()
        => Assert.Equal("dbo.HeapTable.ix_TextField",
                        DataPathText.GetObjectName(new PlanNode { Schema = "dbo", Table = "HeapTable", Index = "ix_TextField" }));

    [Fact]
    public void Uses_Are_Listed_In_A_Fixed_Order()
        => Assert.Equal("Build Key, Predicate", DataPathText.GetUses(ColumnUseType.Predicate | ColumnUseType.BuildKey));

    [Fact]
    public void An_Expression_Input_Is_Not_A_Use()
        => Assert.Equal(string.Empty, DataPathText.GetUses(ColumnUseType.Expression));
}
