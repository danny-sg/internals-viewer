using InternalsViewer.Query.FullTrace.RowBuffers.HashRows;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class HashTableColumnsTests
{
    [Fact]
    public void A_Hash_Table_Row_Holds_The_Build_Columns_Then_The_Build_Keys()
    {
        var hashMatch = new PlanNode
        {
            HashInfo = new HashInfo { BuildKeys = [Column("Id")] },
            Children = [new PlanNode { OutputColumns = [Column("Id"), Column("TextField"), Column("RowSpacer")] }]
        };

        Assert.Equal(["Id", "TextField", "RowSpacer", "Id"], HashTableColumns.Get(hashMatch).Select(c => c.Column));
    }

    [Fact]
    public void The_Build_Keys_Are_Counted_From_The_Hash_Keys()
    {
        var hashMatch = new PlanNode { HashInfo = new HashInfo { BuildKeys = [Column("A"), Column("B")] } };

        Assert.Equal(2, HashTableColumns.GetKeyCount(hashMatch));
    }

    [Fact]
    public void A_Hash_Match_Without_Inputs_Has_No_Columns()
        => Assert.Empty(HashTableColumns.Get(new PlanNode()));

    private static ColumnReference Column(string name) => new() { Table = "[ClusteredTable]", Column = name };
}
