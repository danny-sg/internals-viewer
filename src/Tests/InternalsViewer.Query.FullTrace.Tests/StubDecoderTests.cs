using InternalsViewer.Query.FullTrace.Stubs;
using InternalsViewer.Query.FullTrace.Stubs.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class StubDecoderTests
{
    private const ulong Address = 0x7FFE_0980_0000;

    private const int Rax = 0;

    private const int Rcx = 1;

    private const int R10 = 10;

    [Fact]
    public void A_Virtual_Forwarding_Stub_Jumps_Through_The_Objects_Vtable()
    {
        var stub = Decode(0x48, 0x8B, 0x01, 0x48, 0xFF, 0x60, 0x58);

        Assert.Equal([new StubOperation(StubOperationType.Load, Rax, Rcx, 0, 0),
                      new StubOperation(StubOperationType.JumpMemory, 0, Rax, 0x58, 0)],
                     stub!.Operations);
    }

    [Fact]
    public void A_Wrapper_Stub_Follows_The_Inner_Object_Before_Its_Vtable()
    {
        var stub = Decode(0x48, 0x8B, 0x49, 0x08, 0x48, 0x8B, 0x01, 0x4C, 0x8B, 0x50, 0x50, 0x49, 0xFF, 0xE2);

        Assert.Equal([new StubOperation(StubOperationType.Load, Rcx, Rcx, 0x08, 0),
                      new StubOperation(StubOperationType.Load, Rax, Rcx, 0, 0),
                      new StubOperation(StubOperationType.Load, R10, Rax, 0x50, 0),
                      new StubOperation(StubOperationType.JumpRegister, 0, R10, 0, 0)],
                     stub!.Operations);
    }

    [Fact]
    public void A_Control_Flow_Guard_Dispatch_Jumps_To_The_Target_In_Rax()
    {
        var stub = Decode(0xFF, 0xE0);

        Assert.Equal([new StubOperation(StubOperationType.JumpRegister, 0, Rax, 0, 0)], stub!.Operations);
    }

    [Fact]
    public void An_Import_Thunk_Jumps_Through_Its_Import_Slot()
    {
        var stub = Decode(0xFF, 0x25, 0x00, 0x10, 0x00, 0x00);

        var operation = Assert.Single(stub!.Operations);

        Assert.Equal((StubOperationType.JumpMemory, true, Address + 6 + 0x1000), (operation.Type, operation.IsAbsolute, operation.Address));
    }

    [Fact]
    public void An_Adjustor_Thunk_Moves_This_Then_Jumps_To_The_Method()
    {
        var stub = Decode(0x48, 0x83, 0xC1, 0xF0, 0xE9, 0x10, 0x00, 0x00, 0x00);

        Assert.Equal([new StubOperation(StubOperationType.Add, Rcx, Rcx, -16, 0),
                      new StubOperation(StubOperationType.JumpDirect, 0, StubOperation.AbsoluteSource, 0, Address + 9 + 0x10)],
                     stub!.Operations);
    }

    [Fact]
    public void A_Function_With_A_Prologue_Is_Not_A_Stub()
        => Assert.Null(Decode(0x48, 0x89, 0x5C, 0x24, 0x08, 0x57, 0x48, 0x83, 0xEC, 0x20));

    [Fact]
    public void A_Function_That_Calls_Is_Not_A_Stub()
        => Assert.Null(Decode(0x48, 0x8B, 0x01, 0xFF, 0x50, 0x08, 0xC3));

    [Fact]
    public void A_Stub_That_Writes_The_Stack_Pointer_Is_Not_A_Stub()
        => Assert.Null(Decode(0x48, 0x8B, 0x21, 0xFF, 0xE0));

    [Fact]
    public void Truncated_Code_Is_Not_A_Stub()
        => Assert.Null(Decode(0x48, 0x8B));

    private static Stub? Decode(params byte[] code) => StubDecoder.Decode(code, Address);
}
