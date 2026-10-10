using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class FunctionSignatureTests
{
    [Fact]
    public void A_Member_Function_Is_Read_From_Its_Decorated_Name()
    {
        var signature = FunctionSignature.Parse("CQScanRangeNew::SetRange(unsigned __int64 *)",
                                                "?SetRange@CQScanRangeNew@@IEAAXPEA_K@Z");

        Assert.NotNull(signature);
        Assert.Equal(FunctionKind.Member, signature.Kind);
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
        Assert.Equal(FunctionKind.Static, signature.Kind);
        Assert.Equal("long", signature.ReturnType);
        Assert.Equal(["CQScanNew *", "unsigned __int64 *", "unsigned __int64 *", "bool"], signature.Parameters);
    }

    [Fact]
    public void A_Virtual_Member_Returning_Bool_Is_Decoded()
    {
        var signature = FunctionSignature.Parse("CFoo::IsReady(void)", "?IsReady@CFoo@@UEBA_NXZ");

        Assert.NotNull(signature);
        Assert.Equal(FunctionKind.Member, signature.Kind);
        Assert.Equal("bool", signature.ReturnType);
        Assert.Empty(signature.Parameters);
    }

    [Fact]
    public void A_Free_Function_Has_No_This()
    {
        var signature = FunctionSignature.Parse("GetHoBtLockInternal(int)", "?GetHoBtLockInternal@@YAHH@Z");

        Assert.NotNull(signature);
        Assert.Equal(FunctionKind.Free, signature.Kind);
        Assert.Equal("int", signature.ReturnType);
    }

    [Fact]
    public void A_Static_Member_Of_A_Template_Is_Found_Past_The_Template_Arguments()
    {
        var signature = FunctionSignature.Parse("Filter<int>::Apply(int)", "?Apply@?$Filter@H@@SAXH@Z");

        Assert.NotNull(signature);
        Assert.Equal(FunctionKind.Static, signature.Kind);
    }

    [Fact]
    public void Without_A_Decorated_Name_A_Scoped_Function_Is_Assumed_To_Be_A_Member()
    {
        var signature = FunctionSignature.Parse("CFoo::Bar(int)", null);

        Assert.NotNull(signature);
        Assert.Equal(FunctionKind.Member, signature.Kind);
        Assert.Null(signature.ReturnType);
    }

    [Fact]
    public void The_Kind_Of_A_Function_Is_Read_From_Its_Decorated_Name()
    {
        Assert.Equal(FunctionKind.Member, FunctionSignature.KindOf("?IsReady@CFoo@@UEBA_NXZ"));
        Assert.Equal(FunctionKind.Static, FunctionSignature.KindOf("?GetRowOrReQualifyHelper@CQScanNew@@SAJPEAV1@PEA_K1_N@Z"));
        Assert.Equal(FunctionKind.Free, FunctionSignature.KindOf("?GetHoBtLockInternal@@YAHH@Z"));
        Assert.Null(FunctionSignature.KindOf("GetHoBtLockInternal"));
    }

    [Fact]
    public void Template_Arguments_Do_Not_Split_Parameters()
    {
        Assert.Equal(["Map<int,long>", "char *"], FunctionSignature.ParseParameters("Foo(Map<int,long>,char *)"));
    }

    [Fact]
    public void A_Signature_Without_A_Parameter_List_Is_Not_Parsed()
    {
        Assert.Null(FunctionSignature.Parse("CFoo::Bar", null));
    }
}
