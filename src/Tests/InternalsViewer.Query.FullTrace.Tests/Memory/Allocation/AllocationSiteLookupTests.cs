using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Memory.Allocation;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;

[Trait("Category", "Unit")]
public class AllocationSiteLookupTests
{
    [Fact]
    public void A_Tracking_Overload_Has_Its_File_And_Line_After_The_Memory_Object()
    {
        var layout = Layout("unsigned __int64", "IMemObj *", "char const *", "int", "unsigned char");

        Assert.Equal((2, 3), AllocationSiteLookup.GetSiteSlots(layout));
    }

    [Fact]
    public void An_Overload_With_The_Line_On_The_Stack_Is_Found()
    {
        var layout = Layout("unsigned __int64", "IMemObj *", "int", "char const *", "int", "unsigned char");

        Assert.Equal((3, 4), AllocationSiteLookup.GetSiteSlots(layout));
    }

    [Fact]
    public void An_Overload_With_Only_A_File_And_Line_Is_Found()
    {
        Assert.Equal((1, 2), AllocationSiteLookup.GetSiteSlots(Layout("unsigned __int64", "char const *", "int")));
    }

    [Fact]
    public void A_Plain_Allocator_Has_No_Site()
    {
        Assert.Null(AllocationSiteLookup.GetSiteSlots(Layout("unsigned __int64")));
    }

    [Fact]
    public void A_File_Without_A_Line_Is_Not_A_Site()
    {
        Assert.Null(AllocationSiteLookup.GetSiteSlots(Layout("unsigned __int64", "char const *", "unsigned char")));
    }

    private static ArgumentLayout Layout(params string[] parameters)
        => ArgumentLayout.GetLayout(new FunctionSignature(parameters, FunctionType.Free, "void *"));
}
