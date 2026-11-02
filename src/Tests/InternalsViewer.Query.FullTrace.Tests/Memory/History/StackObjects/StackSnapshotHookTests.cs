using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.StackObjects;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History.StackObjects;

[Trait("Category", "Unit")]
public class StackSnapshotHookTests
{
    private const ulong Method = 0x7FF8_0070_1000;

    private const ulong Consumer = 0x7FF8_0070_4000;

    private const ulong Unrelated = 0x7FF8_0070_6000;

    private const ulong StackObject = 0x55_47EF_B300;

    private const ulong HeapObject = 0x21C_0BB4_C180;

    private const ulong StackPointer = 0x55_47EF_B200;

    private const ulong OwnerStackPointer = 0x55_47EF_B400;

    private const uint Thread = 9;

    private static readonly TraceHookFrame[] Frames = [new(5, OwnerStackPointer, 0, 2), new(3, 0x55_47EF_B800, 0, 1)];

    [Fact]
    public void An_Object_Is_Read_As_It_Is_Handed_Over_And_Again_When_The_Call_Returns()
    {
        var (hook, replay) = Create();

        replay.Write(StackObject, [1, 2, 3, 4, 5, 6, 7, 8]);

        hook.OnEntry(Call(Method, 10, At(10, 4), [StackObject, 0, 0, 0]), replay);

        replay.Write(StackObject + 2, (byte)9);

        hook.OnReturn(Call(Method, 10, At(20, 6)), replay);

        var snapshots = hook.Build();

        Assert.Equal([(At(10, 3), 0, 8), (At(20, 5), 2, 1)],
                     snapshots.Chunks.SelectMany(c => c.Runs).Select(r => (r.Position, r.Offset, (int)r.Length)));
        Assert.Equal(2, snapshots.SnapshotCount);
    }

    [Fact]
    public void An_Object_Belongs_To_The_Frame_That_Holds_It_And_Ends_With_It()
    {
        var (hook, replay) = Create();

        replay.Write(StackObject, [1, 2, 3, 4]);

        hook.OnEntry(Call(Method, 10, At(10, 4), [StackObject, 0, 0, 0]), replay);

        hook.OnFrameEnded(new TraceHookCall { Id = 5, Position = At(99), Thread = Thread });

        var item = Assert.Single(hook.Build().Objects);

        Assert.Equal((StackObject, (int)(OwnerStackPointer - StackObject), 2, At(10, 3), At(99)),
                     (item.Address, item.Size, item.OwnerCallNodeIndex, item.FirstPosition, item.EndPosition));
        Assert.Equal([(Thread, 5ul)], replay.WatchedFrames);
    }

    [Fact]
    public void An_Object_Stops_Where_The_Next_Object_In_Its_Frame_Starts()
    {
        var (hook, replay) = Create();

        replay.Write(StackObject, new byte[0x40]);

        hook.OnEntry(Call(Consumer, 10, At(10, 4), [0, StackObject + 0x20, 0, 0]), replay);

        hook.OnEntry(Call(Method, 11, At(11, 4), [StackObject, 0, 0, 0]), replay);

        Assert.Equal([(StackObject + 0x20, (int)(OwnerStackPointer - StackObject - 0x20)), (StackObject, 0x20)],
                     hook.Build().Objects.Select(o => (o.Address, o.Size)));
    }

    [Fact]
    public void Only_Objects_On_The_Stack_Handed_To_A_Tracked_Argument_Are_Read()
    {
        var (hook, replay) = Create();

        replay.Write(StackObject, [1, 2, 3, 4]);

        hook.OnEntry(Call(Consumer, 10, At(10, 4), [0, HeapObject, 0, 0]), replay);

        hook.OnEntry(Call(Consumer, 11, At(11, 4), [0, 0x55_47EF_C000, 0, 0]), replay);

        hook.OnEntry(Call(Consumer, 12, At(12, 4), [StackObject, 0, 0, 0]), replay);

        hook.OnEntry(Call(Unrelated, 13, At(13, 4), [StackObject, 0, 0, 0]), replay);

        Assert.Empty(hook.Build().Objects);
    }

    private static (StackSnapshotHook Hook, FakeTraceReplay Replay) Create()
        => (new StackSnapshotHook([new MemoryMapFunction(Method, MemoryMapOperation.Visited),
                                   new MemoryMapFunction(Consumer, MemoryMapOperation.Declared)
                                   {
                                       Declared = [new DeclaredClass(1, "BaseXact")]
                                   }]),
            new FakeTraceReplay { Frames = Frames });

    private static TraceHookCall Call(ulong function, ulong id, TracePosition position, ulong[]? arguments = null)
        => new()
        {
            FunctionAddress = function,
            Id = id,
            Position = position,
            Thread = Thread,
            CallNodeIndex = 7,
            Arguments = arguments ?? [0, 0, 0, 0],
            StackPointer = StackPointer
        };
}
