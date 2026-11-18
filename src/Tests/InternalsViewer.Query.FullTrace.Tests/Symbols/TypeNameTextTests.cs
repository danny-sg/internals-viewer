using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Symbols.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Symbols;

[Trait("Category", "Unit")]
public class TypeNameTextTests
{
    private const string HashWorkItems = "CTDynArray<CHashWorkItem * __ptr64,CFnI_Default<CHashWorkItem * __ptr64>,"
                                         + "CFnD_Noop<CHashWorkItem * __ptr64>,CMemObjAlloc<0> >";

    [Theory]
    [InlineData(HashWorkItems, "CTDynArray<CHashWorkItem*>")]
    [InlineData("TDelayedPartitionedStack<CMsqlExecContext,40>", "TDelayedPartitionedStack<CMsqlExecContext, 40>")]
    [InlineData("MemObjCache<CMemObj>", "MemObjCache<CMemObj>")]
    [InlineData("CQScanHash", "CQScanHash")]
    public void A_Type_Name_Loses_Its_Policy_Arguments_And_Pointer_Qualifiers(string type, string expected)
        => Assert.Equal(expected, TypeNameText.Shorten(type));

    [Theory]
    [InlineData("class CQScanHash const * __ptr64", "CQScanHash*")]
    [InlineData("struct CConstantScan", "CConstantScan")]
    [InlineData("enum LCK_MODE", "LCK_MODE")]
    public void Shortening_Removes_Whole_Qualifier_And_Keyword_Words_Only(string type, string expected)
        => Assert.Equal(expected, TypeNameText.Shorten(type));

    [Theory]
    [InlineData("wchar_t", 2, PrimitiveTypeKind.UnsignedInteger, true)]
    [InlineData("WCHAR", 2, PrimitiveTypeKind.UnsignedInteger, true)]
    [InlineData("long", 4, PrimitiveTypeKind.SignedInteger, false)]
    [InlineData("ULONGLONG", 8, PrimitiveTypeKind.UnsignedInteger, false)]
    [InlineData("double", 8, PrimitiveTypeKind.Float, false)]
    public void A_Primitive_Or_Windows_Alias_Has_A_Size_And_Kind(string name, int size, PrimitiveTypeKind kind, bool isCharacter)
    {
        var primitive = TypeNameText.GetPrimitive(name);

        Assert.NotNull(primitive);

        Assert.Equal((size, kind, isCharacter), (primitive.Size, primitive.Kind, primitive.IsCharacter));
    }

    [Fact]
    public void A_Class_Is_Not_A_Primitive()
    {
        Assert.Null(TypeNameText.GetPrimitive("CQScanHash"));
    }

    [Theory]
    [InlineData("const  unsigned short volatile", "unsigned short")]
    [InlineData("class CQScanHash * __ptr64", "class CQScanHash *")]
    public void Normalising_Drops_Qualifiers_And_Extra_Spaces(string type, string expected)
        => Assert.Equal(expected, TypeNameText.Normalise(type));

    [Theory]
    [InlineData("class CQScanHash", "CQScanHash")]
    [InlineData("struct tagDBBINDING", "tagDBBINDING")]
    [InlineData("enum LCK_MODE", "enum LCK_MODE")]
    [InlineData("classic", "classic")]
    public void Only_A_Class_Or_Struct_Keyword_Is_Removed(string type, string expected)
        => Assert.Equal(expected, TypeNameText.RemoveClassKeyword(type).ToString());

    [Theory]
    [InlineData("class CQScanHash const * __ptr64", "CQScanHash")]
    [InlineData("enum LCK_MODE &", "LCK_MODE")]
    [InlineData("unsigned __int64", "unsigned __int64")]
    public void The_Base_Type_Is_The_Name_Before_Any_Indirection(string type, string expected)
        => Assert.Equal(expected, TypeNameText.GetBaseType(type));
}
