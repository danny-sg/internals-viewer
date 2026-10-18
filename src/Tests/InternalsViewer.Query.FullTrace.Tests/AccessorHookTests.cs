using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.RowBuffers.Accessors;
using InternalsViewer.Query.FullTrace.RowSources;
using InternalsViewer.Query.FullTrace.RowSources.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class AccessorHookTests
{
    private const uint Thread = 7;

    private const ulong Constructor = 0x5000;

    private const ulong CreateAccessor = 0x6000;


    private const ulong Iterator = 0x1F4_0000_1000;

    private const ulong Bindings = 0x1F4_0000_7000;

    private const ulong TypeInfo = 0x1F4_0000_8000;

    private const ulong Stack = 0x1F4_0000_A000;

    private const ulong ReturnAddress = 0x7FF8_0000_1234;

    private const ulong HandlePointer = 0x1F4_0000_B000;

    private const ulong Accessor = 0x1F4_0000_C000;

    [Fact]
    public void A_Binding_Created_While_An_Iterator_Is_Built_Gives_That_Iterators_Column_Type()
    {
        var accessors = CreateAccessors(new FakeTraceReplay(), Iterator, 0x08);

        Assert.Equal(new RowBufferType(2, 56, 4, 10, 0, 0), accessors.GetColumnType(Iterator, 0x08));
    }

    [Fact]
    public void A_Binding_Of_An_Unknown_Type_Is_Ignored()
    {
        var accessors = CreateAccessors(new FakeTraceReplay(), Iterator, 0x08, typeId: 0xFF);

        Assert.Null(accessors.GetColumnType(Iterator, 0x08));
    }

    [Fact]
    public void An_Accessor_Created_For_One_Column_Gives_The_Ordinal_Of_That_Column()
        => Assert.Equal(3, CreateHandle(Accessor, 3).GetOrdinal(Accessor));

    [Fact]
    public void An_Accessor_That_Was_Not_Created_In_The_Trace_Has_No_Ordinal()
        => Assert.Equal(-1, CreateHandle(Accessor, 3).GetOrdinal(Accessor + 0x10));

    internal static AccessorHook CreateHandle(ulong accessor, int ordinal)
    {
        var replay = new FakeTraceReplay().Write(Stack, ReturnAddress)
                                          .Write(Stack + 0x30, HandlePointer)
                                          .Write(Bindings, (ulong)ordinal)
                                          .Write(Bindings + 0x08, 0x10UL)
                                          .Write(Bindings + 0x38, 1u);

        var accessors = new AccessorHook(new AccessorFunctions([CreateAccessor]), new RowSourceHook(RowSourceFunctions.Empty));

        accessors.OnEntry(Call(CreateAccessor, 2, [0x1F4_0000_9000, 2, 1, Bindings], Stack), replay);

        replay.Write(HandlePointer, accessor);

        accessors.OnReturn(Call(CreateAccessor, 2, [0x1F4_0000_9000, 2, 1, Bindings], Stack), replay);

        return accessors;
    }

    internal static AccessorHook CreateAccessors(FakeTraceReplay replay, ulong iterator, ulong valueOffset, byte typeId = 56)
    {
        replay.Write(Bindings, 2UL)
              .Write(Bindings + 0x08, valueOffset)
              .Write(Bindings + 0x20, TypeInfo)
              .Write(Bindings + 0x38, 5u)
              .Write(TypeInfo + 0x04, typeId)
              .Write(TypeInfo + 0x10, (ushort)4)
              .Write(TypeInfo + 0x12, (byte)10)
              .Write(TypeInfo + 0x13, (byte)0)
              .Write(TypeInfo + 0x14, 0u);

        var rowSources = new RowSourceHook(new RowSourceFunctions(new Dictionary<ulong, RowSourceFunctionType>
                                                                  {
                                                                      [Constructor] = RowSourceFunctionType.Constructor
                                                                  }));

        var accessors = new AccessorHook(new AccessorFunctions([CreateAccessor]), rowSources);

        rowSources.OnEntry(Call(Constructor, 1, [iterator, 0, 0, 0]), replay);

        accessors.OnEntry(Call(CreateAccessor, 2, [0x1F4_0000_9000, 2, 1, Bindings]), replay);

        rowSources.OnReturn(Call(Constructor, 1, [iterator, 0, 0, 0]), replay);

        return accessors;
    }

    private static TraceHookCall Call(ulong function, ulong id, ulong[] arguments, ulong stackPointer = 0)
        => new()
        {
            Function = function,
            Id = id,
            Call = (uint)id,
            Position = At(id),
            Thread = Thread,
            Arguments = arguments,
            StackPointer = stackPointer,
            ReturnAddress = ReturnAddress
        };
}
