using InternalsViewer.Query.FullTrace.Symbols;

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
}
