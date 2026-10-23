using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack.Locators.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Arguments;

using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;

namespace InternalsViewer.Query.FullTrace.Tests.Locators;

[Trait("Category", "Unit")]
public class LocatorHookTests
{
    private const ulong Function = 0x7FF8_0060_0000;

    private const ulong Page336 = 0x0000_0001_0000_0150;

    private const ulong Pointer = 0x21C_0BB4_C180;

    private const ulong Inner = 0x21C_0BB5_0000;

    private const ulong StackPointer = 0x55_47EF_B230;

    private const ulong ReturnAddress = 0x7FF8_0061_0010;

    private const ulong ReturnedFlag = TraceCallColumns.ReturnedFlag;

    [Fact]
    public void A_Pointer_Argument_Is_Read_When_The_Call_Is_Made()
    {
        var replay = new FakeTraceReplay().Write(Pointer, Page336);

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.Page), replay).GetLocator(1);

        Assert.Equal((LocatorValueType.Page, Page336), (locator?.ValueType, locator?.Value));
    }

    [Fact]
    public void A_Path_Is_Followed_Before_The_Value_Is_Read()
    {
        var replay = new FakeTraceReplay().Write(Pointer + 8, Inner).Write(Inner + 0x10, Page336);

        var read = new LocatorRead(Function, 0, false, LocatorValueType.Page, Offset: 0x10, Path: [8]);

        Assert.Equal(Page336, Replay(read, replay, [Pointer, 0, 0, 0]).GetLocator(0)?.Value);
    }

    [Fact]
    public void A_Stack_Argument_Is_Read_Past_The_Return_Address_And_Home_Space()
    {
        var replay = new FakeTraceReplay().Write(StackPointer, ReturnAddress).Write(StackPointer + 8 + 32, 0x77ul);

        var call = Replay(new LocatorRead(Function, 4, false, LocatorValueType.Argument), replay);

        Assert.Equal(0x77ul, call.GetStackValue(4));
    }

    [Fact]
    public void A_Stack_Argument_Without_A_Return_Address_Is_Read_From_The_Home_Space()
    {
        var replay = new FakeTraceReplay().Write(StackPointer, 0ul).Write(StackPointer + 32, 0x66ul);

        var call = Replay(new LocatorRead(Function, 4, false, LocatorValueType.Argument), replay);

        Assert.Equal(0x66ul, call.GetStackValue(4));
    }

    [Fact]
    public void A_Return_Value_Is_Read_When_The_Call_Returns()
    {
        var register = ArgumentLayout.ReturnRegister;

        var replay = new FakeTraceReplay().Write(Pointer, Page336);

        var store = new TraceCallStore.Builder();

        store.Add(Function, 0, Chunk(Row(1, flags: ReturnedFlag)), 1);

        var hook = new LocatorHook([new LocatorRead(Function, register, true, LocatorValueType.Page)], store);

        hook.OnEntry(Call([0, 0, 0, 0]), replay);

        hook.OnReturn(Call([0, 0, 0, 0], returnValue: Pointer), replay);

        Assert.Equal(Page336, store.Build().GetCallList(Function, 0)![0].GetLocator(register)?.Value);
    }

    [Fact]
    public void A_Call_Left_Without_Returning_Reads_Nothing_At_Return()
    {
        var register = ArgumentLayout.ReturnRegister;

        var store = new TraceCallStore.Builder();

        store.Add(Function, 0, Chunk(Row(1)), 1);

        var hook = new LocatorHook([new LocatorRead(Function, register, true, LocatorValueType.Page)], store);

        var replay = new FakeTraceReplay().Write(Pointer, Page336);

        hook.OnEntry(Call([0, 0, 0, 0]), replay);

        hook.OnAbandoned(Call([0, 0, 0, 0]));

        hook.OnReturn(Call([0, 0, 0, 0], returnValue: Pointer), replay);

        Assert.Null(store.Build().GetCallList(Function, 0)![0].GetLocator(register));
    }

    [Fact]
    public void A_Narrow_String_Ends_At_Its_Terminator()
    {
        var replay = new FakeTraceReplay().Write(Pointer, Encoding.Latin1.GetBytes("dbo.Orders\0garbage"));

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.Text), replay).GetLocator(1);

        Assert.Equal(("dbo.Orders", 0ul), (locator?.Text, locator?.Detail));
    }

    [Fact]
    public void A_Wide_String_Ends_At_A_Two_Byte_Terminator()
    {
        var replay = new FakeTraceReplay().Write(Pointer, Encoding.Unicode.GetBytes("Orders\0x"));

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.WideText), replay).GetLocator(1);

        Assert.Equal(("Orders", 0ul), (locator?.Text, locator?.Detail));
    }

    [Fact]
    public void A_String_Cut_Short_By_The_Trace_Is_Truncated()
    {
        var replay = new FakeTraceReplay().Write(Pointer, Encoding.Latin1.GetBytes("abcdefgh"));

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.Text), replay).GetLocator(1);

        Assert.Equal("abcdefgh (Truncated)", locator?.ToString());
    }

    [Fact]
    public void A_String_The_Trace_Never_Read_Is_Read_From_The_Module_File()
    {
        const ulong imageBase = 0x7FF0_0000_0000;

        var path = Path.Combine(Environment.SystemDirectory, "kernel32.dll");

        using var images = new ImageText([TraceModule.Read(path, imageBase, 0x1000_0000)]);

        var pointer = imageBase + GetRva(path, "CreateFileW\0"u8);

        var call = Replay(new LocatorRead(Function, 0, false, LocatorValueType.Text), new FakeTraceReplay(), [pointer, 0, 0, 0], images);

        Assert.Equal("CreateFileW", call.GetLocator(0)?.ToString());
    }

    [Fact]
    public void A_Type_Info_Is_Read_From_Its_Type_And_Its_Length()
    {
        var replay = new FakeTraceReplay().Write(Pointer, 0x0000_00E7_0000_00E7ul).Write(Pointer + 16, 0xFFFF_FFFF_0000_0100ul);

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.TypeInfo), replay).GetLocator(1);

        Assert.Equal("nvarchar(128)", locator?.ToString());
    }

    [Fact]
    public void A_Type_Info_Whose_Length_Is_Not_In_The_Trace_Shows_Its_Type()
    {
        var replay = new FakeTraceReplay().Write(Pointer, (ushort)0x0138);

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.TypeInfo), replay).GetLocator(1);

        Assert.Equal("int NOT NULL", locator?.ToString());
    }

    [Fact]
    public void A_Log_Sequence_Number_Is_Read_From_Its_Two_Words()
    {
        var replay = new FakeTraceReplay().Write(Pointer, 37 | (152ul << 32)).Write(Pointer + 8, 0xFFFF_0000_0002ul);

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.LogSequenceNumber), replay).GetLocator(1);

        Assert.NotNull(locator);

        Assert.Equal((new LogSequenceNumber(37, 152, 2), "(37:152:2)"), (new LogSequenceNumberLocator(locator.Value).Lsn, locator.ToString()));
    }

    [Fact]
    public void An_Inline_Variant_Shows_Its_Value()
    {
        var replay = new FakeTraceReplay().Write(Pointer, (byte)0).Write(Pointer + 8, 42ul).Write(Pointer + 0x10, 0u);

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.Variant), replay).GetLocator(1);

        Assert.NotNull(locator);

        var variant = new VariantLocator(locator.Value);

        Assert.Equal((VariantStatus.Value, 0u, "0x2A (42)"), (variant.Status, variant.Length, locator.ToString()));
    }

    [Fact]
    public void A_Null_Variant_Shows_Null()
    {
        var replay = new FakeTraceReplay().Write(Pointer, (byte)3).Write(Pointer + 8, 0ul).Write(Pointer + 0x10, 0u);

        Assert.Equal("NULL", Replay(new LocatorRead(Function, 1, false, LocatorValueType.Variant), replay).GetLocator(1)?.ToString());
    }

    [Fact]
    public void A_Variant_Held_By_Reference_Shows_Its_Pointer()
    {
        var replay = new FakeTraceReplay().Write(Pointer, (byte)0).Write(Pointer + 8, Inner).Write(Pointer + 0x10, 12u);

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.Variant), replay).GetLocator(1);

        Assert.NotNull(locator);

        Assert.Equal((12u, $"0x{Inner:X}"), (new VariantLocator(locator.Value).Length, locator.ToString()));
    }

    [Fact]
    public void A_Variant_Whose_Length_Is_Not_In_The_Trace_Is_Read_As_Inline()
    {
        var replay = new FakeTraceReplay().Write(Pointer, (byte)0).Write(Pointer + 8, 1ul);

        Assert.Equal("0x1 (1)", Replay(new LocatorRead(Function, 1, false, LocatorValueType.Variant), replay).GetLocator(1)?.ToString());
    }

    [Fact]
    public void A_Variant_Without_Its_Status_In_The_Trace_Is_Not_Read()
    {
        var replay = new FakeTraceReplay().Write(Pointer + 8, 1ul);

        Assert.Null(Replay(new LocatorRead(Function, 1, false, LocatorValueType.Variant), replay).GetLocator(1));
    }

    [Fact]
    public void Bindings_Are_Read_For_The_Count_Before_Them()
    {
        var replay = new FakeTraceReplay().Write(Pointer, Binding(1, 3, 0x1, 0x0))
                                          .Write(Pointer + BindingLocator.Size, Binding(2, 130, 0x7, 0x8, 0x108, 0x110));

        var read = new LocatorRead(Function, 2, false, LocatorValueType.Bindings, CountRegister: 1);

        var locator = Replay(read, replay, [0, 2, Pointer, 0]).GetLocator(2);

        Assert.Equal("1: I4 Value +0x0 | 2: WSTR Value +0x8 Length +0x108 Status +0x110", locator?.ToString());
    }

    [Fact]
    public void Bindings_The_Trace_Never_Read_Are_Counted()
    {
        var replay = new FakeTraceReplay().Write(Pointer, Binding(1, 3, 0x1, 0x0));

        var read = new LocatorRead(Function, 2, false, LocatorValueType.Bindings, CountRegister: 1);

        Assert.Equal("1: I4 Value +0x0 | 2 More", Replay(read, replay, [0, 3, Pointer, 0]).GetLocator(2)?.ToString());
    }

    [Fact]
    public void A_Binding_Without_A_Count_Is_Read_Alone()
    {
        var replay = new FakeTraceReplay().Write(Pointer, Binding(1, 3, 0x1, 0x0))
                                          .Write(Pointer + BindingLocator.Size, Binding(2, 3, 0x1, 0x4));

        var locator = Replay(new LocatorRead(Function, 1, false, LocatorValueType.Bindings), replay).GetLocator(1);

        Assert.Equal("1: I4 Value +0x0", locator?.ToString());
    }

    private static byte[] Binding(ulong ordinal, ushort type, uint parts, ulong value, ulong length = 0, ulong status = 0)
    {
        var bytes = new byte[BindingLocator.Size];

        BinaryPrimitives.WriteUInt64LittleEndian(bytes, ordinal);

        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0x08), value);

        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0x10), length);

        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0x18), status);

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x38), parts);

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x54), type);

        return bytes;
    }

    private static TraceArgumentCall Replay(LocatorRead read,
                                            FakeTraceReplay replay,
                                            ulong[]? arguments = null,
                                            ImageText? images = null)
    {
        var store = new TraceCallStore.Builder();

        store.Add(Function, 0, Chunk(Row(1)), 1);

        new LocatorHook([read], store, images).OnEntry(Call(arguments ?? [0, Pointer, 0, 0]), replay);

        return store.Build().GetCallList(Function, 0)![0];
    }

    private static TraceHookCall Call(ulong[] arguments, ulong returnValue = 0)
        => new()
        {
            FunctionAddress = Function,
            Id = 40,
            CallIndex = 0,
            Arguments = arguments,
            ReturnValue = returnValue,
            StackPointer = StackPointer,
            ReturnAddress = ReturnAddress
        };

    private static uint GetRva(string path, ReadOnlySpan<byte> text)
    {
        var offset = File.ReadAllBytes(path).AsSpan().IndexOf(text);

        using var stream = File.OpenRead(path);

        var section = new PEHeaders(stream).SectionHeaders.First(s => offset >= s.PointerToRawData
                                                                      && offset < s.PointerToRawData + s.SizeOfRawData);

        return (uint)(section.VirtualAddress + offset - section.PointerToRawData);
    }
}
