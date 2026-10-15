using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryMapHookTests
{
    private const ulong Constructor = 0x7FF8_0070_0000;

    private const ulong Method = 0x7FF8_0070_1000;

    private const ulong OperatorNew = 0x7FF8_0070_2000;

    private const ulong StackObject = 0x55_47EF_B300;

    private const ulong HeapObject = 0x21C_0BB4_C180;

    private const ulong StackPointer = 0x55_47EF_B200;

    private const uint Thread = 9;

    private static readonly TraceHookFrame[] Frames = [new(5, 0x55_47EF_B400, 0, 2), new(3, 0x55_47EF_B800, 0, 1)];

    [Fact]
    public void An_Object_On_The_Stack_Is_Owned_By_The_Frame_That_Holds_It()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Constructor, 10, arguments: [StackObject, 0, 0, 0]), replay);

        var entry = Assert.Single(hook.Build());

        Assert.Equal((StackObject, 10ul, 5ul, MemoryMapOperation.Constructed),
                     (entry.Address, entry.Call, entry.Owner, entry.Operation));
        Assert.Equal([(Thread, 5ul)], replay.WatchedFrames);
    }

    [Fact]
    public void An_Owned_Object_Ends_With_Its_Frame()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Constructor, 10, arguments: [StackObject, 0, 0, 0]), replay);

        hook.OnFrameEnded(new TraceHookCall { Id = 5, CallCount = 99, Thread = Thread });

        Assert.Equal(99ul, Assert.Single(hook.Build()).Until);
    }

    [Fact]
    public void An_Allocation_Is_Recorded_When_It_Returns()
    {
        var (hook, replay) = Create();

        hook.OnReturn(Call(OperatorNew, 20, returnValue: HeapObject), replay);

        var entry = Assert.Single(hook.Build());

        Assert.Equal((HeapObject, 20ul, 0ul, MemoryMapOperation.Allocated),
                     (entry.Address, entry.Call, entry.Owner, entry.Operation));
        Assert.Empty(replay.WatchedFrames);
    }

    [Fact]
    public void The_Outermost_Allocation_Of_An_Address_Is_Kept()
    {
        var (hook, replay) = Create();

        hook.OnReturn(Call(OperatorNew, 20, returnValue: HeapObject), replay);

        hook.OnReturn(Call(OperatorNew, 12, returnValue: HeapObject), replay);

        Assert.Equal(12ul, Assert.Single(hook.Build()).Call);
    }

    [Fact]
    public void A_Visit_Only_Counts_On_Allocated_Memory()
    {
        var (hook, replay) = Create();

        hook.OnEntry(Call(Constructor, 10, arguments: [HeapObject, 0, 0, 0]), replay);

        hook.OnEntry(Call(Method, 11, arguments: [HeapObject, 0, 0, 0]), replay);

        Assert.Single(hook.Build());
    }

    private static (MemoryMapHook Hook, FakeTraceReplay Replay) Create()
        => (new MemoryMapHook([new MemoryMapFunction(Constructor, MemoryMapOperation.Constructed),
                               new MemoryMapFunction(Method, MemoryMapOperation.Visited),
                               new MemoryMapFunction(OperatorNew, MemoryMapOperation.Allocated)]),
            new FakeTraceReplay { Frames = Frames });

    private static TraceHookCall Call(ulong function, ulong id, ulong[]? arguments = null, ulong returnValue = 0)
        => new()
        {
            Function = function,
            Id = id,
            Thread = Thread,
            Node = 7,
            Arguments = arguments ?? [0, 0, 0, 0],
            ReturnValue = returnValue,
            StackPointer = StackPointer
        };
}
