using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.Tests.Helpers;

[Trait("Category", "Unit")]
[Trait("Area", "Columnstore")]
public class ColumnstoreStructureTextTests
{
    private static readonly PageAddress Page = new(1, 100);

    private static ColumnstorePageRead Segment(int rowGroupId, int columnId, string columnName)
        => new(Page, rowGroupId, columnId, columnName, -1, ColumnstoreReadType.Segment);

    private static ColumnstorePageRead Dictionary(int rowGroupId, int columnId, string columnName, int dictionaryId)
        => new(Page, rowGroupId, columnId, columnName, dictionaryId, ColumnstoreReadType.Dictionary);

    [Fact]
    public void No_Reads_Describe_As_Null()
    {
        Assert.Null(ColumnstoreStructureText.Describe([]));
    }

    [Fact]
    public void A_Single_Segment_Names_Its_Row_Group_And_Column()
    {
        var text = ColumnstoreStructureText.Describe([Segment(1, 4, "Region")]);

        Assert.Equal("Row Group 1, Region (4)", text);
    }

    [Fact]
    public void Two_Structures_On_One_Page_Share_A_Row_Group_Heading()
    {
        var text = ColumnstoreStructureText.Describe([Segment(0, 7, "CustomerId"), Segment(0, 4, "Region")]);

        Assert.Equal("Row Group 0, CustomerId (7) / Region (4)", text);
    }

    [Fact]
    public void Separate_Row_Groups_Are_Split_By_A_Semicolon()
    {
        var text = ColumnstoreStructureText.Describe([Segment(1, 4, "Region"), Segment(0, 4, "Region")]);

        Assert.Equal("Row Group 0, Region (4); Row Group 1, Region (4)", text);
    }

    [Fact]
    public void A_Local_Dictionary_Sits_Under_Its_Row_Group()
    {
        var text = ColumnstoreStructureText.Describe([Dictionary(2, 4, "Region", 1)]);

        Assert.Equal("Row Group 2, Region (4) Dictionary 1", text);
    }

    [Fact]
    public void A_Global_Dictionary_Has_No_Row_Group_Heading()
    {
        var text = ColumnstoreStructureText.Describe([Dictionary(-1, 4, "Region", 0)]);

        Assert.Equal("Region (4) Dictionary 0", text);
    }

    [Fact]
    public void A_Column_Without_A_Name_Falls_Back_To_Its_Id()
    {
        var text = ColumnstoreStructureText.Describe([Segment(0, 9, string.Empty)]);

        Assert.Equal("Row Group 0, Column 9", text);
    }

    [Fact]
    public void The_Same_Structure_Read_Twice_Is_Listed_Once()
    {
        var text = ColumnstoreStructureText.Describe([Segment(0, 4, "Region"), Segment(0, 4, "Region")]);

        Assert.Equal("Row Group 0, Region (4)", text);
    }
}
