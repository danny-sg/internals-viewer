using InternalsViewer.UI.App.Helpers;

namespace InternalsViewer.UI.App.Tests.Helpers;

[Trait("Category", "Unit")]
[Trait("Area", "Helpers")]
public class PointerArrayTextTests
{
    [Fact]
    public void Pointers_To_Different_Types_List_Each_Target()
        => Assert.Equal("Pointer[RowsetNewSS.Row, CXVariant, CXVariant]",
                        PointerArrayText.GetLabel(["RowsetNewSS.Row", "CXVariant", "CXVariant"], isArray: true));

    [Fact]
    public void Pointers_To_One_Type_Name_The_Type_Once()
        => Assert.Equal("Pointer<CXVariant>[]", PointerArrayText.GetLabel(["CXVariant", "CXVariant"], isArray: true));

    [Fact]
    public void A_Single_Pointer_Names_Its_Target_Type()
        => Assert.Equal("Pointer<CXVariant>", PointerArrayText.GetLabel(["CXVariant"], isArray: false));

    [Fact]
    public void A_Long_List_Shows_The_First_Targets_And_A_Count()
        => Assert.Equal("Pointer[A, B, C, D, +2 More]", PointerArrayText.GetLabel(["A", "B", "C", "D", "E", "F"], isArray: true));
}
