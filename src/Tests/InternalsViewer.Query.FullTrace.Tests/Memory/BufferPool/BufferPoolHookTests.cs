using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Hooks.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.BufferPool.Enums;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class BufferPoolHookTests
{
    private const ulong Get = 0x7FFA_A079_F370;

    private const ulong PostRead = 0x7FFA_A07C_B8C0;

    private const ulong Release = 0x7FFA_A079_F640;

    private const ulong Buffer = 0x216_A5DE_33C0;

    private const ulong Frame = 0x20A_5448_0000;

    private const ulong NextFrame = 0x20A_5448_2000;

    private const ulong FileControlBlock = 0x261_90E4_22C0;

    private const ulong OtherFileControlBlock = 0x261_90E4_2500;

    [Fact]
    public void Every_Get_And_Page_Read_Is_Hooked_At_Its_Entry_And_Return_And_A_Latch_Release_At_Its_Entry()
        => Assert.Equal([new TraceHookFunction(Get, TraceHookEvents.Entry | TraceHookEvents.Return),
                         new TraceHookFunction(PostRead, TraceHookEvents.Entry | TraceHookEvents.Return),
                         new TraceHookFunction(Release, TraceHookEvents.Entry)],
                        Create().Functions);

    [Fact]
    public void A_Fetched_Page_Reads_Its_Header_And_Slots_When_Its_Latch_Is_Released()
    {
        var hook = Create();

        var replay = Replay();

        hook.OnReturn(Return(Buffer, 5), replay);

        replay.Write(Frame + 0x01, (byte)PageType.Iam)
              .Write(Frame + 0x16, (ushort)2)
              .Write(Frame + 0x1FFE, (ushort)0x60)
              .Write(Frame + 0x1FFC, (ushort)0x8C);

        hook.OnEntry(LatchRelease(Buffer, 8), replay);

        var layout = hook.Build().GetLayout(new PageAddress(1, 100), 6);

        Assert.Equal((PageType.Iam, 2), (layout?.PageHeader.PageType, layout?.OffsetTable.Length));
    }

    [Fact]
    public void A_Slot_The_Engine_Never_Read_Is_Left_Unknown()
    {
        var hook = Create();

        var replay = Replay().Write(Frame + 0x16, (ushort)3).Write(Frame + 0x1FFE, (ushort)0x60).Write(Frame + 0x1FFC, (ushort)0x90);

        hook.OnReturn(Return(Buffer, 5), replay);

        hook.OnEntry(LatchRelease(Buffer, 8), replay);

        Assert.Equal([0x60, 0x90, 0], hook.Build().GetLayout(new PageAddress(1, 100), 6)?.OffsetTable ?? []);
    }

    [Fact]
    public void A_Fetched_Buffer_Records_Its_Frame_Page_And_Database()
    {
        var hook = Create();

        hook.OnReturn(Return(Buffer, 5), Replay());

        Assert.Equal(new TraceBufferReference(new PageAddress(1, 100), 6, 0x60), hook.Build().GetReference(Frame + 0x60, At(6)));
    }

    [Fact]
    public void A_Fetched_Page_Records_The_Record_Offset_Of_Each_Slot()
    {
        var hook = Create();

        var replay = Replay().Write(Frame + 0x16, (ushort)2).Write(Frame + 0x1FFE, (ushort)0x60).Write(Frame + 0x1FFC, (ushort)0x90);

        hook.OnReturn(Return(Buffer, 5), replay);

        Assert.Equal([0x60, 0x90], hook.Build().GetLayout(new PageAddress(1, 100), 6)?.OffsetTable ?? []);
    }

    [Fact]
    public void A_Fetched_Page_Records_The_Page_Type_From_Its_Header()
    {
        var hook = Create();

        var replay = Replay().Write(Frame + 0x01, (byte)PageType.Iam)
                             .Write(Frame + 0x16, (ushort)2)
                             .Write(Frame + 0x1FFE, (ushort)0x60)
                             .Write(Frame + 0x1FFC, (ushort)0x8C);

        hook.OnReturn(Return(Buffer, 5), replay);

        Assert.Equal(PageType.Iam, hook.Build().GetLayout(new PageAddress(1, 100), 6)?.PageHeader.PageType);
    }

    [Fact]
    public void A_Page_Read_Ahead_Records_Its_Slot_Offsets_Once_Its_Database_Is_Known()
    {
        var hook = Create();

        var replay = Replay().Write(NextFrame + 0x16, (ushort)1).Write(NextFrame + 0x1FFE, (ushort)0x60);

        Read(hook, replay, 10, FileControlBlock, Frame, 100);

        Read(hook, replay, 20, FileControlBlock, NextFrame, 101);

        hook.OnReturn(Return(Buffer, 30), replay);

        Assert.Equal([0x60], hook.Build().GetLayout(new PageAddress(1, 101), 6)?.OffsetTable ?? []);
    }

    [Fact]
    public void A_Fetched_Page_Is_In_Its_Frame_From_The_Start_Of_The_Get()
    {
        var hook = Create();

        hook.OnEntry(GetCall(3), new FakeTraceReplay());

        hook.OnReturn(Return(Buffer, 5), Replay());

        Assert.Equal(new PageAddress(1, 100), hook.Build().GetReference(Frame, At(4))?.Page);
    }

    [Fact]
    public void A_Page_Read_Alongside_A_Fetched_Page_Is_In_Its_Frame_From_The_Read()
    {
        var hook = Create();

        var replay = Replay();

        Read(hook, replay, 10, FileControlBlock, Frame, 100);

        Read(hook, replay, 20, FileControlBlock, NextFrame, 101);

        hook.OnReturn(Return(Buffer, 30), replay);

        Assert.Equal(new TraceBufferReference(new PageAddress(1, 101), 6, 0x10), hook.Build().GetReference(NextFrame + 0x10, At(21)));
    }

    [Fact]
    public void A_Page_Read_From_A_File_No_Fetch_Identified_Is_Not_Recorded()
    {
        var hook = Create();

        var replay = Replay();

        Read(hook, replay, 10, FileControlBlock, Frame, 100);

        Read(hook, replay, 20, OtherFileControlBlock, NextFrame, 101);

        hook.OnReturn(Return(Buffer, 30), replay);

        Assert.Null(hook.Build().GetReference(NextFrame, At(25)));
    }

    [Fact]
    public void A_Fetched_Buffer_Is_A_Block_From_Its_First_Fetch()
    {
        var hook = Create();

        hook.OnReturn(Return(Buffer, 9), Replay());

        hook.OnEntry(new TraceHookCall { FunctionAddress = Get, Id = 1, Thread = 7, Position = At(3) }, new FakeTraceReplay());

        hook.OnReturn(new TraceHookCall { FunctionAddress = Get, Id = 1, Thread = 7, ReturnValue = Buffer, Position = At(5) }, Replay());

        Assert.Equal([new MemoryBlock(Buffer, BufferDescriptor.Size, 7, At(3), TracePosition.Unknown)], hook.GetBuffers());
    }

    [Fact]
    public void A_Return_That_Is_Not_A_Page_Buffer_Is_Ignored()
    {
        var hook = Create();

        var replay = new FakeTraceReplay().Write(Buffer, Frame + 0x10)
                                          .Write(Buffer + 0x2C, (ushort)6)
                                          .Write(Buffer + 0x70, 100)
                                          .Write(Buffer + 0x74, (short)1);

        hook.OnReturn(Return(Buffer, 5), replay);

        hook.OnReturn(Return(0, 6), replay);

        Assert.Equal(0, hook.Build().Count);
    }

    private static BufferPoolHook Create()
        => new(new(new Dictionary<ulong, BufferPoolFunctionType>
        {
            [Get] = BufferPoolFunctionType.Get,
            [PostRead] = BufferPoolFunctionType.PostRead,
            [Release] = BufferPoolFunctionType.LatchRelease
        }));

    private static TraceHookCall LatchRelease(ulong buffer, ulong sequence)
        => new() { FunctionAddress = Release, Id = 9, Position = At(sequence), Arguments = new[] { buffer + 0x98 } };

    private static FakeTraceReplay Replay()
        => new FakeTraceReplay().Write(Buffer, Frame)
                                .Write(Buffer + 0x2C, (ushort)6)
                                .Write(Buffer + 0x70, 100)
                                .Write(Buffer + 0x74, (short)1)
                                .Write(Frame + 0x20, 100u)
                                .Write(Frame + 0x24, (short)1)
                                .Write(NextFrame + 0x20, 101u)
                                .Write(NextFrame + 0x24, (short)1);

    private static void Read(BufferPoolHook hook, FakeTraceReplay replay, ulong sequence, ulong fileControlBlock, ulong frame, uint page)
    {
        var id = sequence;

        hook.OnEntry(new TraceHookCall
                     {
                         FunctionAddress = PostRead,
                         Id = id,
                         Position = At(sequence),
                         Arguments = new[] { fileControlBlock, frame, page, 0ul }
                     },
                     replay);

        hook.OnReturn(new TraceHookCall { FunctionAddress = PostRead, Id = id, Position = At(sequence + 1) }, replay);
    }

    private static TraceHookCall GetCall(ulong sequence) => new() { FunctionAddress = Get, Id = 1, Position = At(sequence) };

    private static TraceHookCall Return(ulong buffer, ulong sequence)
        => new() { FunctionAddress = Get, Id = 1, ReturnValue = buffer, Position = At(sequence) };
}
