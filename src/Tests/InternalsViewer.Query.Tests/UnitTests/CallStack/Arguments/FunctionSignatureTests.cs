using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.Tests.UnitTests.CallStack.Arguments;

[Trait("Category", "Unit")]
public class FunctionSignatureTests
{
    [Fact]
    public void A_Member_Function_Is_Read_From_Its_Decorated_Name()
    {
        var signature = FunctionSignature.Parse("CQScanRangeNew::SetRange(unsigned __int64 *)",
                                                "?SetRange@CQScanRangeNew@@IEAAXPEA_K@Z");

        Assert.NotNull(signature);
        Assert.Equal(FunctionType.Member, signature.Type);
        Assert.Equal("void", signature.ReturnType);
        Assert.Equal(["unsigned __int64 *"], signature.Parameters);
    }

    [Fact]
    public void A_Static_Member_Function_Has_No_This()
    {
        var signature = FunctionSignature.Parse(
            "CQScanNew::GetRowOrReQualifyHelper(CQScanNew *,unsigned __int64 *,unsigned __int64 *,bool)",
            "?GetRowOrReQualifyHelper@CQScanNew@@SAJPEAV1@PEA_K1_N@Z");

        Assert.NotNull(signature);
        Assert.Equal(FunctionType.Static, signature.Type);
        Assert.Equal("long", signature.ReturnType);
        Assert.Equal(["CQScanNew *", "unsigned __int64 *", "unsigned __int64 *", "bool"], signature.Parameters);
    }

    [Fact]
    public void Enum_Parameters_Are_Marked_From_The_Decorated_Name()
    {
        var signature = FunctionSignature.Parse(
            "BPool::Get(RecoveryUnit &,PageId const &,int &,LatchBase::LATCH_TYPE,LatchSuspendInfo *,bool)",
            "?Get@BPool@@QEAAPEAUBUF@@AEAVRecoveryUnit@@AEBVPageId@@AEAHW4LATCH_TYPE@LatchBase@@PEAVLatchSuspendInfo@@_N@Z");

        Assert.NotNull(signature);
        Assert.Equal(["RecoveryUnit &", "PageId const &", "int &", "enum LatchBase::LATCH_TYPE", "LatchSuspendInfo *", "bool"],
                     signature.Parameters);
    }

    [Fact]
    public void An_Enum_Scoped_By_A_Name_Back_Reference_Is_Marked()
    {
        var signature = FunctionSignature.Parse("LatchBase::Release(LatchBase::LATCH_TYPE)", "?Release@LatchBase@@QEAA_NW4LATCH_TYPE@1@@Z");

        Assert.NotNull(signature);
        Assert.Equal(["enum LatchBase::LATCH_TYPE"], signature.Parameters);
        Assert.Equal("bool", signature.ReturnType);
    }

    [Fact]
    public void An_Enum_Returned_By_Value_Is_Named_With_Its_Scope()
    {
        var signature = FunctionSignature.Parse(
            "LatchBase::AcquireInternal(LatchBase::LATCH_TYPE,unsigned long,LatchSuspendInfo *,LatchBase::SubLatchInfo *,"
            + "LatchBase::Releasor,unsigned __int64 *,SOS_LsAccessCache *,LatchBase::LatchYieldBehavior)",
            "?AcquireInternal@LatchBase@@AEAA?AW4AcquireResult@1@W4LATCH_TYPE@1@KPEAVLatchSuspendInfo@@PEAUSubLatchInfo@1@"
            + "W4Releasor@1@PEA_KPEAVSOS_LsAccessCache@@W4LatchYieldBehavior@1@@Z");

        Assert.NotNull(signature);
        Assert.Equal("enum LatchBase::AcquireResult", signature.ReturnType);
        Assert.Equal(["enum LatchBase::LATCH_TYPE", "enum LatchBase::Releasor", "enum LatchBase::LatchYieldBehavior"],
                     signature.Parameters.Where(p => p.StartsWith(FunctionSignature.EnumPrefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void A_Class_Returned_By_Value_Is_Named_And_Written_Through_A_Buffer()
    {
        var signature = FunctionSignature.Parse("BlobNode::ChildPID(int)", "?ChildPID@BlobNode@@QEBA?AVPageId@@H@Z");

        Assert.NotNull(signature);
        Assert.Equal("PageId", signature.ReturnType);
        Assert.True(signature.HasReturnBuffer);
    }

    [Fact]
    public void A_Pointer_To_A_Class_Is_Named()
    {
        var signature = FunctionSignature.Parse("Record::GetBackPtr(void)", "?GetBackPtr@Record@@QEBAPEFBURowId@@XZ");

        Assert.NotNull(signature);
        Assert.Equal("RowId const *", signature.ReturnType);
        Assert.False(signature.HasReturnBuffer);
    }

    [Fact]
    public void A_Virtual_Member_Returning_Bool_Is_Decoded()
    {
        var signature = FunctionSignature.Parse("CFoo::IsReady(void)", "?IsReady@CFoo@@UEBA_NXZ");

        Assert.NotNull(signature);
        Assert.Equal(FunctionType.Member, signature.Type);
        Assert.Equal("bool", signature.ReturnType);
        Assert.Empty(signature.Parameters);
    }

    [Fact]
    public void A_Free_Function_Has_No_This()
    {
        var signature = FunctionSignature.Parse("GetHoBtLockInternal(int)", "?GetHoBtLockInternal@@YAHH@Z");

        Assert.NotNull(signature);
        Assert.Equal(FunctionType.Free, signature.Type);
        Assert.Equal("int", signature.ReturnType);
    }

    [Fact]
    public void A_Static_Member_Of_A_Template_Is_Found_Past_The_Template_Arguments()
    {
        var signature = FunctionSignature.Parse("Filter<int>::Apply(int)", "?Apply@?$Filter@H@@SAXH@Z");

        Assert.NotNull(signature);
        Assert.Equal(FunctionType.Static, signature.Type);
    }

    [Fact]
    public void Without_A_Decorated_Name_A_Scoped_Function_Is_Assumed_To_Be_A_Member()
    {
        var signature = FunctionSignature.Parse("CFoo::Bar(int)", null);

        Assert.NotNull(signature);
        Assert.Equal(FunctionType.Member, signature.Type);
        Assert.Null(signature.ReturnType);
    }

    [Fact]
    public void The_Type_Of_A_Function_Is_Read_From_Its_Decorated_Name()
    {
        Assert.Equal(FunctionType.Member, FunctionSignature.GetFunctionType("?IsReady@CFoo@@UEBA_NXZ"));
        Assert.Equal(FunctionType.Static, FunctionSignature.GetFunctionType("?GetRowOrReQualifyHelper@CQScanNew@@SAJPEAV1@PEA_K1_N@Z"));
        Assert.Equal(FunctionType.Free, FunctionSignature.GetFunctionType("?GetHoBtLockInternal@@YAHH@Z"));
        Assert.Null(FunctionSignature.GetFunctionType("GetHoBtLockInternal"));
    }

    [Fact]
    public void Template_Arguments_Do_Not_Split_Parameters()
    {
        Assert.Equal(["Map<int,long>", "char *"], FunctionSignature.ParseParameters("Foo(Map<int,long>,char *)"));
    }

    [Fact]
    public void An_Enum_Passed_By_Reference_Is_Marked_As_An_Enum()
    {
        var signature = FunctionSignature.Parse("GetHoBtLockInternal(XDES *,unsigned __int64,HoBtAccess &,LCK_MODE &)",
                                                "?GetHoBtLockInternal@@YA?AW4LCK_RESULT@@PEAVXDES@@_KAEAVHoBtAccess@@AEAW4LCK_MODE@@@Z");

        Assert.NotNull(signature);
        Assert.Equal(["XDES *", "unsigned __int64", "HoBtAccess &", "enum LCK_MODE &"], signature.Parameters);
        Assert.Equal("enum LCK_RESULT", signature.ReturnType);
    }

    [Fact]
    public void A_Signature_Without_A_Parameter_List_Is_Not_Parsed()
    {
        Assert.Null(FunctionSignature.Parse("CFoo::Bar", null));
    }
}
