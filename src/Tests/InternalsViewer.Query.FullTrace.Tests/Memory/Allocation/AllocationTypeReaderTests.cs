using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Memory.Allocation;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;

[Trait("Category", "Unit")]
public class AllocationTypeReaderTests
{
    [Theory]
    [InlineData("StdAlloc<CRawMemObjAlloc<0> >::AllocArray<CRowsetColDef * __ptr64>", 32, "CRowsetColDef*[4]")]
    [InlineData("CTDynArray<COptExpr * __ptr64,CFnI_Default<COptExpr * __ptr64>,CMemObjAlloc<0> >::UlAdd", 16, "COptExpr*[2]")]
    [InlineData("StdAlloc<CRawMemObjAlloc<0> >::AllocArray<enum JOINTYPE>", 12, "JOINTYPE[3]")]
    [InlineData("StdAlloc<CRawMemObjAlloc<0> >::AllocArray<unsigned short>", 10, "unsigned short[5]")]
    [InlineData("StdAlloc<CRawMemObjAlloc<0> >::AllocArray<CRowsetColDef>", 48, "CRowsetColDef[]")]
    public void An_Array_Allocator_Names_Its_Element_Type_And_Count(string function, int size, string expected)
        => Assert.Equal(expected, AllocationTypeReader.GetLabel([Frame("OperatorNew"), Frame(function)], size));

    [Fact]
    public void An_Object_Allocator_Names_Its_Type_Without_Its_Policy_Arguments()
        => Assert.Equal("CTHashTableBase<Pair<ParamID*, CValRefAlgVar*>>",
                        AllocationTypeReader.GetLabel([Frame("StdAlloc<CRawMemObjAlloc<0> >::Alloc<CTHashTableBase<"
                                                             + "Pair<ParamID * __ptr64,CValRefAlgVar * __ptr64>,"
                                                             + "CTMap<ParamID * __ptr64,CValRefAlgVar * __ptr64>::CEntryHashFn,"
                                                             + "FnCompare,CFnH_Pointer,CMemObjAlloc<0> > >")],
                                                      64));

    [Fact]
    public void An_Allocation_Without_A_Template_Has_No_Type()
        => Assert.Null(AllocationTypeReader.GetLabel([Frame("OperatorNew"), Frame("CXStmtQuery::SetupQueryScanAndExpression")], 16));

    private static FrameFunction Frame(string name) => FrameFunction.Parse("sqlmin", name);
}
