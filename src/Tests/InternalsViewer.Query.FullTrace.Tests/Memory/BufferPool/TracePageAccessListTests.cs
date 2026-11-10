using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Capture;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.StackObjects;
using InternalsViewer.Query.FullTrace.Memory.History.Capture.Watches;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class TracePageAccessListTests
{
    private const ulong Frame = 0x20A_5448_0000;

    private const ulong Buffer = 0x14A_BB17_8340;

    private const ulong Holder = 0x1000;

    private const ulong StackAddress = 0x55_47EF_B300;

    private const ulong OtherFrame = 0x20A_5448_2000;

    private const ulong OtherBuffer = 0x14A_BB17_8400;

    private const ushort Database = 6;

    private static readonly PageAddress Page = new(1, 405);

    private static readonly PageAddress OtherPage = new(1, 406);

    [Fact]
    public void A_Fetched_Page_Is_An_Access_Without_An_Offset()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3).Build();

        var accesses = Read(TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, Pool()));

        Assert.Equal([new TracePageAccess(At(2), Page, Database, null, 0, 0)], accesses);
    }

    [Fact]
    public void A_Member_Called_On_A_Record_Is_An_Access_At_The_Record_Offset()
    {
        var calls = new Recording().Call("Record", "HasNullbitMap", sequence: 5, rcx: Frame + 0x5B3).Build();

        var accesses = Read(TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, Pool()));

        Assert.Equal([new TracePageAccess(At(5), Page, Database, 0x5B3, 0, 0)], accesses);
    }

    [Fact]
    public void Free_Functions_Page_Headers_And_Registers_After_This_Are_Not_Accesses()
    {
        var calls = new Recording().Call(null, "memcpy", sequence: 5, rcx: Frame + 0x5B3)
                                   .Call("Page", "GetLsn", sequence: 6, rcx: Frame + 0x10)
                                   .Call("Record", "HasNullbitMap", sequence: 7, rcx: 0x5000, rdx: Frame + 0x5B4)
                                   .Build();

        Assert.Empty(Read(TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, Pool())));
    }

    [Fact]
    public void A_Pointer_Written_Into_A_Page_Body_Is_An_Access_At_Its_Offset()
    {
        var history = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10)], [(4, Holder, Pointer(Frame + 0x5E2))]);

        Assert.Equal([new TracePageAccess(At(4), Page, Database, 0x5E2, -1, -1)], Read(TracePageAccessList.Build(Empty(), history, Pool())));
    }

    [Fact]
    public void A_Pointer_Moved_By_Its_Low_Bytes_Is_Read_Whole_After_Each_Write()
    {
        var history = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10)],
                                                     [(4, Holder, Pointer(Frame + 0x5E2)), (6, Holder, Pointer(Frame + 0x611))]);

        Assert.Equal([0x5E2, 0x611], Read(TracePageAccessList.Build(Empty(), history, Pool())).Select(a => a.Offset));
    }

    [Fact]
    public void A_Stack_Object_Holding_A_Row_Not_Yet_Read_From_Its_Page_Is_An_Access()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3).Build();

        var accesses = Read(TracePageAccessList.Build(calls, Snapshot(Frame + 0x5E2), Pool()));

        Assert.Equal([null, 0x5E2, 0x611], accesses.Select(a => a.Offset));
    }

    [Fact]
    public void A_Stack_Object_Holding_A_Row_Already_Read_From_Its_Page_Is_Not_An_Access()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3)
                                   .Call("Record", "HasNullbitMap", sequence: 3, rcx: Frame + 0x5E2)
                                   .Call("Record", "HasNullbitMap", sequence: 4, rcx: Frame + 0x700)
                                   .Build();

        var accesses = Read(TracePageAccessList.Build(calls, Snapshot(Frame + 0x5E2), Pool()));

        Assert.Equal([At(2), At(3), At(4), At(9)], accesses.Select(a => a.Position));
    }

    [Fact]
    public void A_Stack_Object_Holding_A_Row_Of_An_Earlier_Page_Is_Not_An_Access()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3)
                                   .Call("BPool", "Get", sequence: 4, rcx: 0x5000, returnValue: OtherBuffer, endSequence: 5)
                                   .Build();

        var accesses = Read(TracePageAccessList.Build(calls, Snapshot(Frame + 0x5E2), Pool()));

        Assert.Equal([At(2), At(4), At(9)], accesses.Select(a => a.Position));
    }

    [Fact]
    public void Page_Reads_Replace_Record_Calls_And_Written_Pointers()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3)
                                   .Call("Record", "HasNullbitMap", sequence: 5, rcx: Frame + 0x5B3)
                                   .Build();

        var history = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10)], [(4, Holder, Pointer(Frame + 0x5E2))]);

        var reads = new TracePageReadList([new TracePageRead(At(6), 9, Frame + 0x611, 2)]);

        var accesses = Read(TracePageAccessList.Build(calls, history, LaidOutPool(), reads));

        Assert.Equal([(At(2), (int?)null), (At(6), (int?)0x611)], accesses.Select(a => (a.Position, a.Offset)));
    }

    [Fact]
    public void A_Read_Of_The_Whole_Page_Is_An_Access_To_The_Page_Not_A_Row()
    {
        var reads = new TracePageReadList([new TracePageRead(At(6), 9, Frame + 0x60, 0x2000 - 0x60)]);

        var accesses = Read(TracePageAccessList.Build(Empty(), MemoryHistoryStore.Empty, Pool(), reads));

        Assert.Equal([(At(6), (int?)null)], accesses.Select(a => (a.Position, a.Offset)));
    }

    [Fact]
    public void Reads_While_The_Page_Is_Completed_From_Disk_Are_An_Access_To_The_Page()
    {
        var calls = new Recording().Call("FCB", "PostRead", sequence: 5, rcx: 0, endSequence: 8).Build();

        var reads = Reads((6, 9, 0x158, 8), (7, 9, 0x198, 8), (9, 9, 0x5B3, 4));

        var accesses = Read(TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, LaidOutPool(), reads));

        Assert.Equal([(At(6), (int?)null), (At(9), (int?)0x5B3)], accesses.Select(a => (a.Position, a.Offset)));
    }

    [Fact]
    public void A_Read_Of_A_Page_Whose_Slots_Were_Never_Read_Is_Not_A_Row_Access()
        => Assert.Empty(Read(TracePageAccessList.Build(Empty(), MemoryHistoryStore.Empty, Pool(), Reads((6, 9, 0xC2, 4)))));

    [Theory]
    [InlineData(PageType.Iam)]
    [InlineData(PageType.Pfs)]
    [InlineData(PageType.Gam)]
    public void A_Read_Of_An_Allocation_Page_Is_Not_A_Row_Access(PageType type) => Assert.Empty(RowAccesses(Reads((6, 9, 0x5B3, 4)), type));

    [Theory]
    [InlineData(PageType.Data)]
    [InlineData(PageType.Index)]
    [InlineData(PageType.None)]
    public void A_Read_Of_A_Row_On_A_Data_Or_Index_Page_Is_A_Row_Access(PageType type)
        => Assert.Equal([0x5B3], RowAccesses(Reads((6, 9, 0x5B3, 4)), type).Select(a => a.Offset));

    [Fact]
    public void A_Read_Of_The_Page_Header_Is_A_Header_Access_And_Not_A_Row_Access()
    {
        var accesses = Headers(Reads((6, 9, 0x20, 4)));

        var header = Assert.Single(accesses.HeaderAccesses);

        Assert.Equal((0, At(6), Page), (accesses.Count, header.Access.Position, header.Access.Page));
    }

    [Fact]
    public void A_Header_Access_Names_The_Field_It_Read()
        => Assert.Equal(["Log Sequence Number"], Assert.Single(Headers(Reads((6, 9, 0x28, 10))).HeaderAccesses).GetFields());

    [Fact]
    public void Header_Reads_Merged_Into_One_Access_Name_Every_Field()
        => Assert.Equal(["Page Type", "Slot Count"],
                        Assert.Single(Headers(Reads((6, 9, 0x01, 1), (7, 9, 0x16, 2))).HeaderAccesses).GetFields());

    [Theory]
    [InlineData(0x5F, 1)]
    [InlineData(0x60, 0)]
    public void The_Page_Header_Ends_Before_Byte_96(ulong offset, int expected)
        => Assert.Equal(expected, Headers(Reads((6, 9, offset, 1))).HeaderAccesses.Length);

    [Fact]
    public void Header_Reads_Of_One_Page_In_A_Row_Are_One_Header_Access()
        => Assert.Equal([At(6)], Headers(Reads((6, 9, 0x01, 1), (7, 9, 0x16, 2))).HeaderAccesses.Select(a => a.Access.Position));

    [Fact]
    public void A_Row_Read_Between_Header_Reads_Starts_A_New_Header_Access()
        => Assert.Equal([At(6), At(8)],
                        Headers(Reads((6, 9, 0x16, 2), (7, 9, 0x5B3, 4), (8, 9, 0x16, 2))).HeaderAccesses.Select(a => a.Access.Position));

    [Fact]
    public void A_Header_Read_Between_Reads_Of_One_Row_Leaves_One_Row_Access()
        => Assert.Equal([At(6)], RowAccesses(Reads((6, 9, 0x5B3, 4), (7, 9, 0x16, 2), (8, 9, 0x5C0, 2))).Select(a => a.Position));

    [Fact]
    public void A_Header_Read_While_The_Page_Is_Completed_From_Disk_Is_Not_A_Header_Access()
    {
        var calls = new Recording().Call("FCB", "PostRead", sequence: 5, rcx: 0, endSequence: 8).Build();

        Assert.Empty(TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, LaidOutPool(), Reads((6, 9, 0x10, 8))).HeaderAccesses);
    }

    [Fact]
    public void A_Header_Read_Of_A_Page_Whose_Slots_Were_Never_Read_Is_A_Header_Access()
        => Assert.Single(TracePageAccessList.Build(Empty(), MemoryHistoryStore.Empty, Pool(), Reads((6, 9, 0x16, 2))).HeaderAccesses);

    [Fact]
    public void The_Header_Access_At_A_Position_Is_Found_By_That_Position()
    {
        var accesses = Headers(Reads((6, 9, 0x16, 2)));

        Assert.Equal((Page, (PageAddress?)null, (TracePageAccess?)null),
                     (accesses.GetHeaderAccess(At(6))?.Access.Page,
                      accesses.GetHeaderAccess(At(7))?.Access.Page,
                      accesses.GetRowAccess(At(6))));
    }

    [Fact]
    public void Reads_Inside_One_Row_Are_One_Access_At_The_Start_Of_The_Row()
    {
        var reads = Reads((6, 9, 0x5B3, 4), (7, 9, 0x5C0, 2), (8, 9, 0x5D0, 8));

        Assert.Equal([(At(6), (int?)0x5B3)], RowAccesses(reads).Select(a => (a.Position, a.Offset)));
    }

    [Fact]
    public void A_Slot_Array_Read_Is_Not_A_Row_Access()
    {
        var reads = Reads((6, 9, 0x1FFA, 2), (7, 9, 0x5B3, 4));

        Assert.Equal([(At(7), (int?)0x5B3)], RowAccesses(reads).Select(a => (a.Position, a.Offset)));
    }

    [Fact]
    public void A_Read_Before_The_First_Row_Is_Not_An_Access() => Assert.Empty(RowAccesses(Reads((6, 9, 0x80, 4))));

    [Fact]
    public void One_Row_Read_By_Two_Threads_Is_An_Access_For_Each()
        => Assert.Equal(2, RowAccesses(Reads((6, 9, 0x5B3, 4), (7, 10, 0x5B3, 4), (8, 9, 0x5C0, 2))).Count);

    [Fact]
    public void A_Page_Read_Before_Its_Page_Was_Seen_In_The_Frame_Is_Not_An_Access()
    {
        var reads = new TracePageReadList([new TracePageRead(At(0), 9, OtherFrame + 0x100, 4),
                                           new TracePageRead(At(6), 9, Frame + 0x100, 4)]);

        Assert.Equal([At(6)], RowAccesses(reads).Select(a => a.Position));
    }

    [Fact]
    public void The_Same_Row_Referenced_Twice_In_A_Row_Is_One_Access()
    {
        var history = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10)],
                                                     [(4, Holder, Pointer(Frame + 0x60)), (5, Holder + 8, Pointer(Frame + 0x60))]);

        Assert.Equal([At(4)], Read(TracePageAccessList.Build(Empty(), history, Pool())).Select(a => a.Position));
    }

    [Fact]
    public void Accesses_From_Every_Source_Are_In_Position_Order()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3)
                                   .Call("Record", "HasNullbitMap", sequence: 6, rcx: Frame + 0x5B3)
                                   .Build();

        var history = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10)], [(4, Holder, Pointer(Frame + 0x5E2))]);

        Assert.Equal([At(2), At(4), At(6)], Read(TracePageAccessList.Build(calls, history, Pool())).Select(a => a.Position));
    }

    [Fact]
    public void Each_Access_Names_The_Call_That_Made_It()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3)
                                   .Call("Record", "HasNullbitMap", sequence: 5, rcx: Frame + 0x5B3)
                                   .Call("Record", "HasNullbitMap", sequence: 6, rcx: Frame + 0x611)
                                   .Build();

        var accesses = Read(TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, Pool()));

        Assert.Equal([(0, 0), (1, 0), (1, 1)], accesses.Select(a => (a.CallNodeIndex, a.CallIndex)));
    }

    [Fact]
    public void The_Row_Access_At_A_Position_Skips_A_Page_Fetch()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3)
                                   .Call("Record", "HasNullbitMap", sequence: 6, rcx: Frame + 0x5B3)
                                   .Build();

        var accesses = TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, Pool());

        Assert.Equal(((int?)null, (int?)0x5B3), (accesses.GetRowAccess(At(2))?.Offset, accesses.GetRowAccess(At(6))?.Offset));
    }

    private static TraceBufferPool Pool()
    {
        var builder = new TraceBufferPool.Builder();

        builder.Add(new TraceBufferPage(Frame, Page, Database, At(1), Buffer));

        builder.Add(new TraceBufferPage(OtherFrame, OtherPage, Database, At(1), OtherBuffer));

        return builder.Build();
    }

    private static TraceCallStore Empty() => new Recording().Build();

    private static TracePageReadList Reads(params (ulong Sequence, uint Thread, ulong Offset, ushort Length)[] reads)
        => new([.. reads.Select(r => new TracePageRead(At(r.Sequence), r.Thread, Frame + r.Offset, r.Length))]);

    private static TraceBufferPool LaidOutPool(PageType type = PageType.None)
    {
        var builder = new TraceBufferPool.Builder();

        builder.Add(new TraceBufferPage(Frame, Page, Database, At(1), Buffer));

        builder.Add(new TraceBufferPage(OtherFrame, OtherPage, Database, At(1), OtherBuffer));

        builder.AddLayout(Page, Database, [0x100, 0x5B3, 0x611], type);

        builder.AddLayout(OtherPage, Database, [0x100], type);

        return builder.Build();
    }

    private static List<TracePageAccess> RowAccesses(TracePageReadList reads, PageType type = PageType.None)
        => Read(TracePageAccessList.Build(Empty(), MemoryHistoryStore.Empty, LaidOutPool(type), reads));

    private static TracePageAccessList Headers(TracePageReadList reads)
        => TracePageAccessList.Build(Empty(), MemoryHistoryStore.Empty, LaidOutPool(), reads);

    private static MemoryHistoryStore Snapshot(ulong pointer)
    {
        StackSnapshotRun[] runs = [new(0, At(5), 0, 0, 8), new(0, At(9), 0, 8, 2)];

        byte[] data = [.. Pointer(pointer), .. BitConverter.GetBytes((ushort)0x611)];

        var stack = new StackSnapshotResult([new StackObject(StackAddress, 8, 9, 6, At(5), At(50))], [new(runs, data)], 2);

        var recording = new MemoryHistoryBuilder([], [], new Dictionary<ulong, TracePosition>(), null, stack);

        return recording.Build(WatchResult.Empty).Store;
    }

    private static List<TracePageAccess> Read(TracePageAccessList list) => [.. Enumerable.Range(0, list.Count).Select(i => list[i])];

    private static byte[] Pointer(ulong value) => BitConverter.GetBytes(value);

    private sealed class Recording
    {
        private const ulong ModuleBase = 0x7FF0_0000_0000;

        private const ulong Thread = 9;

        private CallTree Tree { get; } = new();

        private List<CallTreeNode?> Nodes { get; } = [];

        private Dictionary<string, (CallFrame Frame, List<ulong[]> Rows)> Functions { get; } = new();

        public Recording Call(string? className,
                              string method,
                              ulong sequence,
                              ulong rcx,
                              ulong rdx = 0,
                              ulong returnValue = 0,
                              ulong endSequence = ulong.MaxValue)
        {
            var name = $"{className}::{method}";

            if (!Functions.TryGetValue(name, out var function))
            {
                var rva = (uint)(0x1000 + Functions.Count * 0x100);

                var frame = new CallFrame
                {
                    Module = new("sqlmin"),
                    Rva = rva,
                    Address = ModuleBase + rva,
                    Function = new FrameFunction
                    {
                        ClassName = className,
                        MethodName = method,
                        SymbolCategory = SymbolCategory.Unknown
                    }
                };

                Nodes.Add(Tree.AddCall(Tree.Root, frame, 1));

                function = (frame, []);

                Functions[name] = function;
            }

            var flags = endSequence == ulong.MaxValue ? Thread : Thread | TraceCallColumnSet.ReturnedFlag;

            function.Rows.Add(TraceCallRowHelper.Row(sequence,
                                                     flags: flags,
                                                     rcx: rcx,
                                                     rdx: rdx,
                                                     returnValue: returnValue,
                                                     callNodeIndex: Nodes.Count - 1,
                                                     endSequence: endSequence));

            return this;
        }

        public TraceCallStore Build()
        {
            var builder = new TraceCallStore.Builder();

            foreach (var (frame, rows) in Functions.Values)
            {
                builder.Add(frame.Address, frame.InstanceAddress, TraceCallRowHelper.Chunk([.. rows]), rows.Count);
            }

            var store = builder.Build();

            store.MapNodes([.. Nodes]);

            return store;
        }
    }
}
