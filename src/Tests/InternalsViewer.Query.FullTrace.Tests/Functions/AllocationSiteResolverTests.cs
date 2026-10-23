using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.Tests.Functions;

[Trait("Category", "Unit")]
public class AllocationSiteResolverTests
{
    [Fact]
    public void A_Tracking_Overload_Has_Its_File_And_Line_After_The_Memory_Object()
    {
        var signature = Signature("unsigned __int64", "IMemObj *", "char const *", "int", "unsigned char");

        Assert.Equal((2, 3), AllocationSiteResolver.GetSiteSlots(signature));
    }

    [Fact]
    public void An_Overload_With_The_Line_On_The_Stack_Is_Found()
    {
        var signature = Signature("unsigned __int64", "IMemObj *", "int", "char const *", "int", "unsigned char");

        Assert.Equal((3, 4), AllocationSiteResolver.GetSiteSlots(signature));
    }

    [Fact]
    public void An_Overload_With_Only_A_File_And_Line_Is_Found()
    {
        Assert.Equal((1, 2), AllocationSiteResolver.GetSiteSlots(Signature("unsigned __int64", "char const *", "int")));
    }

    [Fact]
    public void A_Plain_Allocator_Has_No_Site()
    {
        Assert.Null(AllocationSiteResolver.GetSiteSlots(Signature("unsigned __int64")));
    }

    [Fact]
    public void A_File_Without_A_Line_Is_Not_A_Site()
    {
        Assert.Null(AllocationSiteResolver.GetSiteSlots(Signature("unsigned __int64", "char const *", "unsigned char")));
    }

    private static FunctionSignature Signature(params string[] parameters) => new(parameters, FunctionType.Free, "void *");
}
