using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class ArgumentLayoutTests
{
    [Fact]
    public void A_Member_Function_Takes_This_In_Rcx_And_Its_First_Argument_In_Rdx()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["unsigned __int64 *"], FunctionType.Member, "void"));

        Assert.Equal(["this", ""], layout.Slots.Select(s => s.Name));
        Assert.Equal(["RCX", "RDX"], layout.Slots.Select(s => s.Source));
    }

    [Theory]
    [InlineData("RCX", true)]
    [InlineData("RDX", false)]
    [InlineData("R8", false)]
    public void A_Member_Function_Without_Arguments_Declares_Only_This(string location, bool isDeclared)
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Member, "void"));

        Assert.Equal(isDeclared, layout.IsDeclared(location));
    }

    [Fact]
    public void A_Floating_Argument_Leaves_Its_Integer_Register_Undeclared()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int", "double"], FunctionType.Free, "void"));

        Assert.Equal((true, false), (layout.IsDeclared("RCX"), layout.IsDeclared("RDX")));
    }

    [Fact]
    public void A_Stack_Argument_Is_Declared_At_Its_Stack_Location()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int", "int", "int", "int", "void *"], FunctionType.Free, "void"));

        Assert.Equal((true, false), (layout.IsDeclared("[RSP+0x28]"), layout.IsDeclared("[RSP+0x30]")));
    }

    [Fact]
    public void Variadic_Arguments_Fill_The_Remaining_Registers()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["char const *", "..."], FunctionType.Free, "int"));

        Assert.Equal((true, true, true), (layout.IsDeclared("RDX"), layout.IsDeclared("R8"), layout.IsDeclared("R9")));
    }

    [Theory]
    [InlineData("void", false)]
    [InlineData("double", false)]
    [InlineData("AccBindings *", true)]
    [InlineData(null, true)]
    public void The_Return_Register_Is_Declared_When_A_Value_Is_Returned_In_It(string? returnType, bool isDeclared)
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([], FunctionType.Free, returnType));

        Assert.Equal(isDeclared, layout.IsDeclared(ArgumentLayout.ReturnRegisterName));
    }

    [Fact]
    public void A_Class_Returned_By_Value_Takes_The_Register_After_This()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int"], FunctionType.Member, "PageId", HasReturnBuffer: true));

        Assert.Equal(["this", ArgumentLayout.ReturnBuffer, ""], layout.Slots.Select(s => s.Name));
        Assert.Equal(["RCX", "RDX", "R8"], layout.Slots.Select(s => s.Source));
        Assert.Equal("PageId *", layout.Slots[1].Type);
    }

    [Fact]
    public void A_Static_Function_Takes_Its_First_Argument_In_Rcx()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["CQScanNew *", "bool"], FunctionType.Static, "long"));

        Assert.Equal(["RCX", "RDX"], layout.Slots.Select(s => s.Source));
    }

    [Fact]
    public void A_Floating_Point_Argument_Uses_The_Xmm_Register_For_Its_Position()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int", "double"], FunctionType.Member, null));

        var argument = layout.Slots[2];

        Assert.Equal(ArgumentLocation.FloatingRegister, argument.Location);
        Assert.Equal("XMM2", argument.Source);
    }

    [Fact]
    public void Arguments_After_The_Fourth_Are_Read_From_The_Stack()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature(["int", "int", "int", "int", "int"], FunctionType.Free, null));

        Assert.Equal(["RCX", "RDX", "R8", "R9", "[RSP+0x28]"], layout.Slots.Select(s => s.Source));
        Assert.Equal(ArgumentLocation.Stack, layout.Slots[4].Location);
    }

    [Fact]
    public void Arguments_Past_The_Captured_Slots_Are_Not_Captured()
    {
        var layout = ArgumentLayout.LayoutOf(new FunctionSignature([.. Enumerable.Repeat("int", 17)], FunctionType.Free, null));

        Assert.Equal((ArgumentLocation.Stack, ArgumentLocation.NotCaptured), (layout.Slots[15].Location, layout.Slots[16].Location));
    }
}
