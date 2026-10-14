using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class ArgumentLayoutTests
{
    [Fact]
    public void A_Member_Function_Takes_This_In_Rcx_And_Its_First_Argument_In_Rdx()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64 *"], FunctionKind.Member, "void"));

        Assert.Equal(["this", ""], layout.Slots.Select(s => s.Name));
        Assert.Equal(["RCX", "RDX"], layout.Slots.Select(s => s.Source));
    }

    [Fact]
    public void A_Class_Returned_By_Value_Takes_The_Register_After_This()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int"], FunctionKind.Member, "PageId", HasReturnBuffer: true));

        Assert.Equal(["this", ArgumentLayout.ReturnBuffer, ""], layout.Slots.Select(s => s.Name));
        Assert.Equal(["RCX", "RDX", "R8"], layout.Slots.Select(s => s.Source));
        Assert.Equal("PageId *", layout.Slots[1].Type);
    }

    [Fact]
    public void A_Static_Function_Takes_Its_First_Argument_In_Rcx()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["CQScanNew *", "bool"], FunctionKind.Static, "long"));

        Assert.Equal(["RCX", "RDX"], layout.Slots.Select(s => s.Source));
    }

    [Fact]
    public void A_Floating_Point_Argument_Uses_The_Xmm_Register_For_Its_Position()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int", "double"], FunctionKind.Member, null));

        var argument = layout.Slots[2];

        Assert.Equal(ArgumentLocation.FloatingRegister, argument.Location);
        Assert.Equal("XMM2", argument.Source);
    }

    [Fact]
    public void Arguments_After_The_Fourth_Are_Read_From_The_Stack()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int", "int", "int", "int", "int"], FunctionKind.Free, null));

        Assert.Equal(["RCX", "RDX", "R8", "R9", "[RSP+0x28]"], layout.Slots.Select(s => s.Source));
        Assert.Equal(ArgumentLocation.Stack, layout.Slots[4].Location);
    }

    [Fact]
    public void Arguments_Past_The_Captured_Slots_Are_Not_Captured()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([.. Enumerable.Repeat("int", 17)], FunctionKind.Free, null));

        Assert.Equal((ArgumentLocation.Stack, ArgumentLocation.NotCaptured), (layout.Slots[15].Location, layout.Slots[16].Location));
    }
}
