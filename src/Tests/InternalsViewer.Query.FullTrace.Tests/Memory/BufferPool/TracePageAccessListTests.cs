using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Tests.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.BufferPool;

[Trait("Category", "Unit")]
public class TracePageAccessListTests
{
    private const ulong Frame = 0x20A_5448_0000;

    private const ulong Buffer = 0x14A_BB17_8340;

    private const ulong Holder = 0x1000;

    private const ushort Database = 6;

    private static readonly PageAddress Page = new(1, 405);

    [Fact]
    public void A_Fetched_Page_Is_An_Access_Without_An_Offset()
    {
        var calls = new Recording().Call("BPool", "Get", sequence: 2, rcx: 0x5000, returnValue: Buffer, endSequence: 3).Build();

        var accesses = Read(TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, Pool()));

        Assert.Equal([new TracePageAccess(At(2), Page, Database, null)], accesses);
    }

    [Fact]
    public void A_Member_Called_On_A_Record_Is_An_Access_At_The_Record_Offset()
    {
        var calls = new Recording().Call("Record", "HasNullbitMap", sequence: 5, rcx: Frame + 0x5B3).Build();

        var accesses = Read(TracePageAccessList.Build(calls, MemoryHistoryStore.Empty, Pool()));

        Assert.Equal([new TracePageAccess(At(5), Page, Database, 0x5B3)], accesses);
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

        Assert.Equal([new TracePageAccess(At(4), Page, Database, 0x5E2)], Read(TracePageAccessList.Build(Empty(), history, Pool())));
    }

    [Fact]
    public void A_Pointer_Moved_By_Its_Low_Bytes_Is_Read_Whole_After_Each_Write()
    {
        var history = MemoryHistoryStoreHelper.Build([MemoryHistoryStoreHelper.Block(Holder, 0x10)],
                                                     [(4, Holder, Pointer(Frame + 0x5E2)), (6, Holder, Pointer(Frame + 0x611))]);

        Assert.Equal([0x5E2, 0x611], Read(TracePageAccessList.Build(Empty(), history, Pool())).Select(a => a.Offset));
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

    private static TraceBufferPool Pool()
    {
        var builder = new TraceBufferPool.Builder();

        builder.Add(new TraceBufferPage(Frame, Page, Database, At(1), Buffer));

        return builder.Build();
    }

    private static TraceCallStore Empty() => new Recording().Build();

    private static List<TracePageAccess> Read(TracePageAccessList list) => [.. Enumerable.Range(0, list.Count).Select(i => list[i])];

    private static byte[] Pointer(ulong value) => BitConverter.GetBytes(value);

    private sealed class Recording
    {
        private const ulong ModuleBase = 0x7FF0_0000_0000;

        private const ulong Thread = 9;

        private CallStackTree Tree { get; } = new();

        private List<CallStackNode?> Nodes { get; } = [];

        private Dictionary<string, (CallstackFrame Frame, List<ulong[]> Rows)> Functions { get; } = new();

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

                var frame = new CallstackFrame
                {
                    Module = "sqlmin",
                    Rva = rva,
                    Address = ModuleBase + rva,
                    Resolved = new ResolvedCallstackFrame
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

            var flags = endSequence == ulong.MaxValue ? Thread : Thread | TraceCallChunk.ReturnedFlag;

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
