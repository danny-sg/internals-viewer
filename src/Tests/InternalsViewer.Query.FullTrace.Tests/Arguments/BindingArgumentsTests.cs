using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Tests.Arguments;

[Trait("Category", "Unit")]
public class BindingArgumentsTests
{
    [Theory]
    [InlineData("enum AccessorFlagEx")]
    [InlineData("AccessorFlagEx")]
    public void The_Flags_Before_The_Extended_Flags_And_The_Count_Are_Accessor_Flags(string extendedFlags)
    {
        var layout = Apply("XDES *", "HoBtAccess *", "unsigned long", extendedFlags, "unsigned __int64", "tagDBBINDING const * const");

        Assert.Equal(BindingArguments.AccessorFlagsType, layout.Slots[3].Type);
    }

    [Fact]
    public void The_Flags_Directly_Before_The_Count_Are_Accessor_Flags()
    {
        var layout = Apply("unsigned long",
                           "unsigned __int64",
                           "tagDBBINDING const * const",
                           "unsigned __int64",
                           "unsigned __int64 *",
                           "unsigned long * const");

        Assert.Equal([BindingArguments.AccessorFlagsType, "unsigned long * const"], [layout.Slots[1].Type, layout.Slots[6].Type]);
    }

    [Fact]
    public void Bindings_Without_A_Count_Leave_Every_Type_Alone()
    {
        var layout = Apply("unsigned long", "tagDBBINDING const *");

        Assert.Equal(["this", "unsigned long", "tagDBBINDING const *"], layout.Slots.Select(s => s.Type));
    }

    [Fact]
    public void A_Count_Without_Flags_Before_It_Leaves_Every_Type_Alone()
    {
        var layout = Apply("HoBtAccess const *", "unsigned __int64", "tagDBBINDING const *");

        Assert.Equal(["this", "HoBtAccess const *", "unsigned __int64", "tagDBBINDING const *"], layout.Slots.Select(s => s.Type));
    }

    private static ArgumentLayout Apply(params string[] parameters)
        => KnownParameters.Apply(ArgumentLayout.GetLayout(new FunctionSignature(parameters, FunctionType.Member, "long")), null, null);
}
