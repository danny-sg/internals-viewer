using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryMapHookTests
{
    private const ulong Constructor = 0x7FF8_0070_0000;

    private const ulong Method = 0x7FF8_0070_1000;

    private const ulong OperatorNew = 0x7FF8_0070_2000;

    private const ulong Factory = 0x7FF8_0070_3000;

    private const ulong Consumer = 0x7FF8_0070_4000;

    private const ulong Getter = 0x7FF8_0070_5000;

    private const ulong StackObject = 0x55_47EF_B300;

    private const ulong HeapObject = 0x21C_0BB4_C180;

    private const ulong Vtable = 0x7FF8_0090_0000;

    private const ulong StackPointer = 0x55_47EF_B200;

    private const uint Thread = 9;

    private static readonly TraceHookFrame[] Frames = [new(5, 0x55_47EF_B400, 0, 2), new(3, 0x55_47EF_B800, 0, 1)];

    [Fact]
    public void An_Object_On_The_Stack_Is_Owned_By_The_Frame_That_Holds_It()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Constructor, 10, arguments: [StackObject, 0, 0, 0]), replay);

        var entry = Assert.Single(hook.Build());

        Assert.Equal((StackObject, 10ul, At(10), 5ul, MemoryMapOperation.Constructed),
                     (entry.Address, entry.Frame, entry.FromPosition, entry.Owner, entry.Operation));
        Assert.Equal([(Thread, 5ul)], replay.WatchedFrames);
    }

    [Fact]
    public void An_Owned_Object_Ends_With_Its_Frame()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Constructor, 10, arguments: [StackObject, 0, 0, 0]), replay);

        hook.OnFrameEnded(new TraceHookCall { Id = 5, Position = At(99), Thread = Thread });

        Assert.Equal(At(99), Assert.Single(hook.Build()).ToPosition);
    }

    [Fact]
    public void An_Allocation_Is_Recorded_When_It_Returns()
    {
        var (hook, replay) = Create();

        hook.OnReturn(Call(OperatorNew, 20, returnValue: HeapObject), replay);

        var entry = Assert.Single(hook.Build());

        Assert.Equal((HeapObject, 20ul, At(20), 0ul, MemoryMapOperation.Allocated),
                     (entry.Address, entry.Frame, entry.FromPosition, entry.Owner, entry.Operation));
        Assert.Empty(replay.WatchedFrames);
    }

    [Fact]
    public void The_Outermost_Allocation_Of_An_Address_Is_Kept()
    {
        var (hook, replay) = Create();

        hook.OnReturn(Call(OperatorNew, 20, returnValue: HeapObject), replay);

        hook.OnReturn(Call(OperatorNew, 12, returnValue: HeapObject), replay);

        Assert.Equal(12ul, Assert.Single(hook.Build()).Frame);
    }

    [Fact]
    public void A_Visit_Only_Counts_On_Allocated_Memory()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Constructor, 10, arguments: [HeapObject, 0, 0, 0]), replay);

        hook.OnEntry(Call(Method, 11, arguments: [HeapObject, 0, 0, 0]), replay);

        Assert.Single(hook.Build());
    }

    [Fact]
    public void A_Declared_Parameter_Names_Memory_Nothing_Else_Has()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Consumer, 10, arguments: [0, HeapObject, 0, 0]), replay);

        var entry = Assert.Single(hook.Build());

        Assert.Equal((HeapObject, MemoryMapOperation.Declared, "BaseXact"), (entry.Address, entry.Operation, entry.ClassName));
    }

    [Fact]
    public void A_Declared_Return_Is_Recorded_When_The_Call_Returns()
    {
        var (hook, replay) = Create();

        hook.OnReturn(Call(Factory, 20, returnValue: HeapObject), replay);

        var entry = Assert.Single(hook.Build());

        Assert.Equal((HeapObject, At(20), MemoryMapOperation.Declared, "AccBindings"),
                     (entry.Address, entry.FromPosition, entry.Operation, entry.ClassName));
    }

    [Fact]
    public void A_Declared_Type_Never_Replaces_A_Member_Call()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Method, 10, arguments: [HeapObject, 0, 0, 0]), replay);

        hook.OnEntry(Call(Consumer, 11, arguments: [0, HeapObject, 0, 0]), replay);

        Assert.Equal(MemoryMapOperation.Visited, Assert.Single(hook.Build()).Operation);
    }

    [Fact]
    public void A_Member_Call_Follows_A_Declared_Type()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Consumer, 10, arguments: [0, HeapObject, 0, 0]), replay);

        hook.OnEntry(Call(Method, 11, arguments: [HeapObject, 0, 0, 0]), replay);

        Assert.Equal([MemoryMapOperation.Declared, MemoryMapOperation.Visited], hook.Build().Select(e => e.Operation));
    }

    [Fact]
    public void A_Function_Is_Hooked_For_Every_Event_Its_Types_Need()
    {
        var (hook, _) = Create();

        var events = hook.Functions.ToDictionary(f => f.Address, f => f.Events);

        Assert.Equal((TraceHookEvents.Entry, TraceHookEvents.Return, TraceHookEvents.Entry | TraceHookEvents.Return),
                     (events[Consumer], events[Factory], events[Getter]));
    }

    [Fact]
    public void An_Unnamed_Allocation_Is_Named_By_Its_Vtable()
    {
        var (hook, replay) = Create();

        hook.OnReturn(Call(OperatorNew, 20, returnValue: HeapObject), replay);

        replay.Write(HeapObject, Vtable);

        hook.OnEntry(Call(Method, 21, arguments: [0, HeapObject, 0, 0]), replay);

        var entry = Assert.Single(hook.Build());

        Assert.Equal((MemoryMapOperation.Allocated, "CQScanTableScanNew"), (entry.Operation, entry.ClassName));
    }

    [Fact]
    public void A_Vtable_Outranks_Later_Member_Calls_And_Declared_Types()
    {
        var (hook, replay) = Create();

        hook.OnReturn(Call(OperatorNew, 20, returnValue: HeapObject), replay);

        replay.Write(HeapObject, Vtable);

        hook.OnEntry(Call(Method, 21, arguments: [HeapObject, 0, 0, 0]), replay);

        hook.OnEntry(Call(Consumer, 22, arguments: [0, HeapObject, 0, 0]), replay);

        Assert.Equal("CQScanTableScanNew", Assert.Single(hook.Build()).ClassName);
    }

    [Fact]
    public void An_Allocation_Is_Read_Once()
    {
        var (hook, replay) = Create();

        hook.OnReturn(Call(OperatorNew, 20, returnValue: HeapObject), replay);

        replay.Write(HeapObject, 0x1234ul);

        hook.OnEntry(Call(Method, 21, arguments: [0, HeapObject, 0, 0]), replay);

        replay.Write(HeapObject, Vtable);

        hook.OnEntry(Call(Method, 22, arguments: [0, HeapObject, 0, 0]), replay);

        Assert.Null(Assert.Single(hook.Build()).ClassName);
    }

    [Fact]
    public void Memory_With_No_Allocation_Is_Not_Read()
    {
        var (hook, replay) = Create();

        replay.Write(HeapObject, Vtable);

        hook.OnEntry(Call(Method, 21, arguments: [0, HeapObject, 0, 0]), replay);

        Assert.Empty(hook.Build());
    }

    private static (MemoryMapHook Hook, FakeTraceReplay Replay) Create()
        => (new MemoryMapHook([new MemoryMapFunction(Constructor, MemoryMapOperation.Constructed),
                               new MemoryMapFunction(Method, MemoryMapOperation.Visited),
                               new MemoryMapFunction(OperatorNew, MemoryMapOperation.Allocated),
                               new MemoryMapFunction(Factory, MemoryMapOperation.Declared)
                               {
                                   Declared = [new DeclaredClass(ArgumentLayout.ReturnRegister, "AccBindings")]
                               },
                               new MemoryMapFunction(Consumer, MemoryMapOperation.Declared)
                               {
                                   Declared = [new DeclaredClass(1, "BaseXact")]
                               },
                               new MemoryMapFunction(Getter, MemoryMapOperation.Visited)
                               {
                                   Declared = [new DeclaredClass(ArgumentLayout.ReturnRegister, "CTypeInfo")]
                               }],
                              new Dictionary<ulong, string> { [Vtable] = "CQScanTableScanNew" }),
            new FakeTraceReplay { Frames = Frames });

    private static TraceHookCall Call(ulong function, ulong id, ulong[]? arguments = null, ulong returnValue = 0)
        => new()
        {
            FunctionAddress = function,
            Id = id,
            Position = At(id),
            Thread = Thread,
            CallNodeIndex = 7,
            Arguments = arguments ?? [0, 0, 0, 0],
            ReturnValue = returnValue,
            StackPointer = StackPointer
        };
}
