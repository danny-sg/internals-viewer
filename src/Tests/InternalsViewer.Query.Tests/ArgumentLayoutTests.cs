using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class ArgumentLayoutTests
{
    [Fact]
    public void A_Member_Function_Takes_This_In_Rcx_And_Its_First_Argument_In_Rdx()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["unsigned __int64 *"], FunctionKind.Member, "void"));

        Assert.Equal(["this", "Argument 1"], layout.Slots.Select(s => s.Name));
        Assert.Equal(["RCX", "RDX"], layout.Slots.Select(s => s.Source));
    }

    [Fact]
    public void A_Static_Function_Takes_Its_First_Argument_In_Rcx()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["CQScanNew *", "bool"], FunctionKind.Static, "long"));

        Assert.Equal(["RCX", "RDX"], layout.Slots.Select(s => s.Source));
    }

    [Fact]
    public void A_Floating_Point_Argument_Uses_The_Xmm_Register_For_Its_Position()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["int", "double"], FunctionKind.Member, null));

        var argument = layout.Slots[2];

        Assert.Equal(ArgumentLocation.FloatingRegister, argument.Location);
        Assert.Equal("XMM2", argument.Source);
    }

    [Fact]
    public void Arguments_After_The_Fourth_Are_Read_From_The_Stack()
    {
        var layout = ArgumentLayout.For(new FunctionSignature(["int", "int", "int", "int", "int"], FunctionKind.Free, null));

        Assert.Equal(["RCX", "RDX", "R8", "R9", "[RSP+0x28]"], layout.Slots.Select(s => s.Source));
        Assert.Equal(ArgumentLocation.Stack, layout.Slots[4].Location);
    }

    [Fact]
    public void Arguments_Past_The_Captured_Slots_Are_Not_Captured()
    {
        var layout = ArgumentLayout.For(new FunctionSignature([.. Enumerable.Repeat("int", 9)], FunctionKind.Free, null));

        Assert.Equal(ArgumentLocation.NotCaptured, layout.Slots[8].Location);
    }
}
