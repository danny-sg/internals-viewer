using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.TimeTravel.Locators;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class LocatorReadsTests
{
    private const ulong Function = 0x1000;

    [Fact]
    public void A_Page_Passed_By_Const_Reference_Is_Read_When_Called()
    {
        var reads = Reads("BPool::Get(RecoveryUnit &,PageId const &,int &,LatchBase::LATCH_TYPE,LatchSuspendInfo *,bool)",
                          "?Get@BPool@@QEAAPEAUBUF@@AEAVRecoveryUnit@@AEBVPageId@@AEAHW4LATCH_TYPE@LatchBase@@"
                          + "PEAVLatchSuspendInfo@@_N@Z");

        Assert.Equal([new LocatorRead(Function, 2, false, LocatorKind.Page)], reads);
    }

    [Fact]
    public void A_Page_Filled_In_Through_A_Reference_Is_Read_On_Return()
    {
        var reads = Reads("IAM_BmapScan::GetNext(PageId &)", "?GetNext@IAM_BmapScan@@QEAAHAEAVPageId@@@Z");

        Assert.Equal([new LocatorRead(Function, 1, true, LocatorKind.Page)], reads);
    }

    [Fact]
    public void Pages_Passed_By_Value_Are_Read_When_Called()
    {
        var reads = Reads("FCB::CheckPageId(PageId,PageId)", "?CheckPageId@FCB@@QEAA_NVPageId@@0@Z");

        Assert.Equal([new LocatorRead(Function, 1, false, LocatorKind.Page), new LocatorRead(Function, 2, false, LocatorKind.Page)],
                     reads);
    }

    [Fact]
    public void A_Row_Passed_By_Value_Is_Read_As_A_Row()
    {
        var reads = Reads("HeapDataSetSession::GetRowByRID(RowId,PageVersionId *,RowAccessOption,unsigned __int64 *)",
                          "?GetRowByRID@HeapDataSetSession@@AEAAXURowId@@PEAVPageVersionId@@W4RowAccessOption@@PEA_K@Z");

        Assert.Equal([new LocatorRead(Function, 1, false, LocatorKind.Row)], reads);
    }

    [Fact]
    public void A_Row_Filled_In_Through_A_Reference_Is_Read_On_Return()
    {
        var reads = Reads("HeapRowObject::GetRidAndPageVersionId(RowId &,PageVersionId *,CheckVisibilityOption)",
                          "?GetRidAndPageVersionId@HeapRowObject@@QEAAXAEAURowId@@PEAVPageVersionId@@W4CheckVisibilityOption@@@Z");

        Assert.Equal([new LocatorRead(Function, 1, true, LocatorKind.Row)], reads);
    }

    [Fact]
    public void A_Page_Returned_By_Value_Is_Read_Through_The_Return_Value()
    {
        var reads = Reads("BlobNode::ChildPID(int)", "?ChildPID@BlobNode@@QEBA?AVPageId@@H@Z");

        Assert.Equal([new LocatorRead(Function, LocatorReads.ReturnRegister, true, LocatorKind.Page)], reads);
    }

    [Fact]
    public void A_Row_Returned_By_Pointer_Is_Read_Through_The_Return_Value()
    {
        var reads = Reads("Record::GetBackPtr(void)", "?GetBackPtr@Record@@QEBAPEFBURowId@@XZ");

        Assert.Equal([new LocatorRead(Function, LocatorReads.ReturnRegister, true, LocatorKind.Row)], reads);
    }

    [Fact]
    public void An_Allocation_Page_Is_Read_From_The_Page_Inside_It()
    {
        var reads = Reads("UnOrderPageScanner::GetNextPage(int,AllocationPageId &)",
                          "?GetNextPage@UnOrderPageScanner@@UEAAXHAEAVAllocationPageId@@@Z");

        Assert.Equal([new LocatorRead(Function, 2, true, LocatorKind.Page, 8)], reads);
    }

    [Fact]
    public void A_Columnstore_Object_Key_Is_Read_As_Three_Words()
    {
        var reads = Reads("ColumnStoreObjectRef::Fix(ColumnStoreObjectKey const &,bool,HoBtAccess *,XDES *,"
                          + "DeserializeFromDiskConstructor *)",
                          "?Fix@ColumnStoreObjectRef@@QEAAPEAVColumnStoreObject@@AEBVColumnStoreObjectKey@@_NPEAVHoBtAccess@@PEAVXDES@@"
                          + "PEAUDeserializeFromDiskConstructor@@@Z");

        Assert.Equal([new LocatorRead(Function, 1, false, LocatorKind.ColumnStoreObject, 0),
                      new LocatorRead(Function, 1, false, LocatorKind.ColumnStoreObject, 8),
                      new LocatorRead(Function, 1, false, LocatorKind.ColumnStoreObject, 16)],
                     reads);
    }

    [Fact]
    public void A_Parameter_After_The_Fourth_Register_Is_Not_Read()
    {
        var signature = new FunctionSignature(["int", "int", "int", "PageId const &"], FunctionKind.Member, "void");

        Assert.Empty(LocatorReads.For(Function, signature));
    }

    [Theory]
    [InlineData("PageId const &", LocatorKind.Page, true, true)]
    [InlineData("PageId", LocatorKind.Page, false, false)]
    [InlineData("RowId &", LocatorKind.Row, true, false)]
    [InlineData("RowId const __unaligned *", LocatorKind.Row, true, true)]
    [InlineData("ColumnStoreObjectKey const &", LocatorKind.ColumnStoreObject, true, true)]
    [InlineData("AllocationPageId const *", LocatorKind.Page, true, true, 8)]
    public void A_Page_Or_Row_Type_Is_Recognised(string type, LocatorKind kind, bool indirect, bool isConst, int offset = 0)
        => Assert.Equal(new LocatorType(kind, indirect, isConst, offset), LocatorType.Of(type));

    [Theory]
    [InlineData("AlignedPageId *")]
    [InlineData("FullRowId const &")]
    [InlineData("PageId * *")]
    [InlineData("PageIdOrderScan &")]
    [InlineData("unsigned __int64")]
    public void Other_Types_Are_Not_Read(string type) => Assert.Null(LocatorType.Of(type));

    private static List<LocatorRead> Reads(string signature, string decoratedName)
        => [.. LocatorReads.For(Function, FunctionSignature.Parse(signature, decoratedName)!)];
}
