using InternalsViewer.Query.FullTrace.Stubs;

namespace InternalsViewer.Query.FullTrace.Tests.Stubs;

[Trait("Category", "Unit")]
public class StubTests
{
    private const ulong ImageStart = 0x7FFE_0980_0000;

    private const ulong ImageEnd = 0x7FFE_0A00_0000;

    private const ulong Object = 0x1F4_0000_1000;

    private const ulong Inner = 0x1F4_0000_2000;

    private const ulong VTable = 0x7FFE_0990_0000;

    private const ulong Target = 0x7FFE_0985_DD40;

    private const int Rcx = 1;

    private static readonly Stub VirtualStub = StubDecoder.Decode([0x48, 0x8B, 0x01, 0x48, 0xFF, 0x60, 0x58], ImageStart)!;

    private static readonly Stub WrapperStub
        = StubDecoder.Decode([0x48, 0x8B, 0x49, 0x08, 0x48, 0x8B, 0x01, 0x4C, 0x8B, 0x50, 0x50, 0x49, 0xFF, 0xE2], ImageStart)!;

    [Fact]
    public void A_Stub_Resolves_To_The_Method_In_The_Objects_Vtable()
    {
        var replay = new FakeTraceReplay().Write(Object, VTable).Write(VTable + 0x58, Target);

        Assert.Equal((true, Target), Resolve(VirtualStub, replay, Registers(Object)));
    }

    [Fact]
    public void A_Wrapper_Stub_Passes_The_Inner_Object_On_As_This()
    {
        var replay = new FakeTraceReplay().Write(Object + 0x08, Inner).Write(Inner, VTable).Write(VTable + 0x50, Target);

        var registers = Registers(Object);

        Assert.Equal((true, Target), Resolve(WrapperStub, replay, registers));
        Assert.Equal(Inner, registers[Rcx]);
    }

    [Fact]
    public void A_Pointer_That_Cannot_Be_Read_Leaves_The_Registers_Unchanged()
    {
        var replay = new FakeTraceReplay().Write(Object + 0x08, Inner);

        var registers = Registers(Object);

        Assert.Equal((false, 0UL), Resolve(WrapperStub, replay, registers));
        Assert.Equal(Object, registers[Rcx]);
    }

    [Fact]
    public void A_Null_Target_Is_Not_Resolved()
    {
        var replay = new FakeTraceReplay().Write(Object, VTable).Write(VTable + 0x58, 0UL);

        Assert.False(Resolve(VirtualStub, replay, Registers(Object)).IsResolved);
    }

    [Fact]
    public void An_Image_Value_Read_Before_Is_Used_When_The_View_Cannot_See_It()
    {
        var memory = new StubMemory([(ImageStart, ImageEnd)]);

        var registers = Registers(Object);

        VirtualStub.TryResolve(registers, new FakeTraceReplay().Write(Object, VTable).Write(VTable + 0x58, Target), memory, out _);

        var isResolved = VirtualStub.TryResolve(registers, new FakeTraceReplay().Write(Object, VTable), memory, out var target);

        Assert.Equal((true, Target), (isResolved, target));
    }

    [Fact]
    public void A_Heap_Value_Is_Never_Reused()
    {
        var memory = new StubMemory([(ImageStart, ImageEnd)]);

        var registers = Registers(Object);

        VirtualStub.TryResolve(registers, new FakeTraceReplay().Write(Object, VTable).Write(VTable + 0x58, Target), memory, out _);

        Assert.False(VirtualStub.TryResolve(registers, new FakeTraceReplay(), memory, out _));
    }

    private static (bool IsResolved, ulong Target) Resolve(Stub stub, FakeTraceReplay replay, ulong[] registers)
    {
        var isResolved = stub.TryResolve(registers, replay, new StubMemory([(ImageStart, ImageEnd)]), out var target);

        return (isResolved, target);
    }

    private static ulong[] Registers(ulong rcx)
    {
        var registers = new ulong[16];

        registers[Rcx] = rcx;

        return registers;
    }
}
