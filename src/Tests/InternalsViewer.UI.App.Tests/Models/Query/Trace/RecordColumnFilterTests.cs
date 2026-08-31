using System.Data;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Models.Query.Trace;

namespace InternalsViewer.UI.App.Tests.Models.Query.Trace;

[Trait("Category", "Unit")]
[Trait("Area", "Trace")]
public class RecordColumnFilterTests
{
    [Fact]
    public void Keeps_A_Column_Each_Side_Of_A_Join_Carries()
    {
        var node = new PlanNode
        {
            NodeId = 1,
            OutputColumns =
            [
                new ColumnReference { Table = "[ProductCategory]", Column = "Name" },
                new ColumnReference { Table = "[ProductSubcategory]", Column = "ProductSubcategoryID" },
                new ColumnReference { Table = "[ProductSubcategory]", Column = "Name" }
            ]
        };

        var joined = Fields("Name", "ProductCategoryID", "ProductSubcategoryID", "ProductCategoryID", "Name", "rowguid", "ModifiedDate");

        var kept = RecordColumnFilter.For(node).Apply(joined).Select(f => f.Name).ToList();

        Assert.Equal(["Name", "ProductSubcategoryID", "Name"], kept);
    }

    [Fact]
    public void Keeps_Every_Column_When_The_Operator_States_None()
    {
        var joined = Fields("Name", "ProductCategoryID");

        var kept = RecordColumnFilter.For(new PlanNode()).Apply(joined).Select(f => f.Name).ToList();

        Assert.Equal(["Name", "ProductCategoryID"], kept);
    }

    private static List<RecordField> Fields(params string[] names)
        => [.. names.Select(n => new FixedVarRecordField(new ColumnStructure { ColumnName = n, DataType = SqlDbType.NVarChar }))];
}
