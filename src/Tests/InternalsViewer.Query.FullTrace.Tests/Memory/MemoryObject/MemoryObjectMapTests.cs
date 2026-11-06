using InternalsViewer.Query.FullTrace.Memory.MemoryObject;
using System.Text;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Tests.Memory.Layouts.Bindings;
using InternalsViewer.Query.Memory;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Symbols.Categories;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.MemoryObject;

[Trait("Category", "Unit")]
public class MemoryObjectMapTests
{
    private const ulong Execute = 0x1A75F280060;

    private const ulong Mark = 0x1A75F280A80;

    private const ulong Context = 0x1A75F281870;

    private const ulong Allocated = 0x1A75F284AF0;

    private const ulong Other = 0x1A765CA4060;

    private const ulong RuntimeStats = 0x29264AD4C60;

    private const ulong OtherRuntimeStats = 0x29264AD5C60;

    private static readonly Dictionary<ulong, MemoryObjectInfo> Types = new() { [Execute] = new MemoryObjectInfo("MEMOBJ_EXECUTE", 8192) };

    [Fact]
    public void An_Address_In_A_Page_A_Memory_Object_Handed_Out_Belongs_To_That_Memory_Object()
    {
        var map = new Recording().Call("CMemProc", "PvMark", sequence: 10, Execute, returned: Mark).Build(Types);

        var owner = map.GetOwner(Context, At(20));

        Assert.Equal((Execute, "Internal Execution Plan (MXC)", "0x1A75F280060 + 0x1810"),
                     (owner?.Address, owner?.Name, owner?.GetText(Context)));
    }

    [Fact]
    public void An_Address_In_Another_Page_Of_The_Memory_Object_Is_Not_An_Offset_From_It()
    {
        var map = new Recording().Call("CMemProc", "Alloc", sequence: 10, Execute, rdx: 0x1D8, returned: Allocated).Build(Types);

        Assert.Equal("0x1A75F284B10", map.GetOwner(Allocated + 0x20, At(20))?.GetText(Allocated + 0x20));
    }

    [Fact]
    public void A_Page_Nothing_Was_Handed_Out_From_Has_No_Owner()
    {
        var map = new Recording().Call("CMemProc", "Alloc", sequence: 10, Execute, rdx: 0x20000, returned: Allocated).Build(Types);

        Assert.Null(map.GetOwner(Context, At(20)));
        Assert.Null(map.GetOwner(0x20010, At(20)));
    }

    [Fact]
    public void A_Pointer_Given_Back_To_A_Memory_Object_Claims_Its_Page()
    {
        var map = new Recording().Call("CMemProc", "FreeToMark", sequence: 10, Execute, rdx: Mark).Build(Types);

        Assert.Equal(Execute, map.GetOwner(Context, At(20))?.Address);
    }

    [Fact]
    public void A_Page_Belongs_To_The_Latest_Memory_Object_To_Claim_It_Before_The_Position()
    {
        var map = new Recording().Call("CMemProc", "PvMark", sequence: 10, Execute, returned: Mark)
                                 .Call("CMemObj", "Alloc", sequence: 30, Other, rdx: 0x40, returned: Mark + 0x100)
                                 .Build(Types);

        Assert.Equal((Execute, Execute, Other),
                     (map.GetOwner(Context, At(5))?.Address,
                      map.GetOwner(Context, At(20))?.Address,
                      map.GetOwner(Context, At(40))?.Address));
    }

    [Fact]
    public void Calls_On_Other_Classes_Claim_Nothing()
    {
        var map = new Recording().Call("CQueryScan", "Alloc", sequence: 10, Execute, returned: Mark).Build(Types);

        Assert.Null(map.GetOwner(Context, At(20)));
    }

    [Fact]
    public void Only_The_Types_Of_Memory_Objects_That_Own_Pages_Are_Written()
    {
        var types = new Dictionary<ulong, MemoryObjectInfo>
        {
            [Execute] = new("MEMOBJ_EXECUTE", 8192),
            [Other] = new("MEMOBJ_SORT", 8192)
        };

        var map = new Recording().Call("CMemProc", "PvMark", sequence: 10, Execute, returned: Mark).Build(types);

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            map.Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.True(MemoryObjectMap.IsSupported(reader));

        stream.Position = 0;

        Assert.Equal([KeyValuePair.Create(Execute, new MemoryObjectInfo("MEMOBJ_EXECUTE", 8192))], MemoryObjectMap.ReadObjects(reader));
    }

    [Fact]
    public void Memory_Objects_With_Small_Pages_Each_Own_Only_Their_Own_Page()
    {
        var types = new Dictionary<ulong, MemoryObjectInfo>
        {
            [RuntimeStats] = new("MEMOBJ_QDSRUNTIMESTATSCACHE", 1024),
            [OtherRuntimeStats] = new("MEMOBJ_QDSRUNTIMESTATSCACHE", 1024)
        };

        var map = new Recording().Call("CMemObj", "Alloc", sequence: 10, RuntimeStats, rdx: 0x40, returned: 0x29264AD4D00)
                                 .Call("CMemObj", "Alloc", sequence: 20, OtherRuntimeStats, rdx: 0x40, returned: 0x29264AD5D00)
                                 .Build(types);

        Assert.Equal("MEMOBJ_QDSRUNTIMESTATSCACHE", map.GetOwner(0x29264AD4D80, At(30))?.Name);

        Assert.Equal((RuntimeStats, OtherRuntimeStats, (ulong?)null),
                     (map.GetOwner(0x29264AD4D80, At(30))?.Address,
                      map.GetOwner(0x29264AD5D80, At(30))?.Address,
                      map.GetOwner(0x29264AD4100, At(30))?.Address));

        Assert.Equal(("0x29264AD4C60 + 0x120", "0x29264AD5C60 + 0x120"),
                     (map.GetOwner(0x29264AD4D80, At(30))?.GetText(0x29264AD4D80),
                      map.GetOwner(0x29264AD5D80, At(30))?.GetText(0x29264AD5D80)));
    }

    [Fact]
    public void Each_Page_A_Memory_Object_Owns_Is_A_Block_From_Its_First_Claim()
    {
        var map = new Recording().Call("CMemProc", "PvMark", sequence: 10, Execute, returned: Mark)
                                 .Call("CMemProc", "Alloc", sequence: 20, Execute, rdx: 0x1D8, returned: Allocated)
                                 .Call("CMemProc", "Alloc", sequence: 30, Execute, rdx: 0x58, returned: Allocated + 0x1E0)
                                 .Build(Types);

        Assert.Equal([(0x1A75F280000ul, 0x2000ul, At(10)), (0x1A75F284000ul, 0x2000ul, At(20))],
                     map.GetPageBlocks().Select(b => (b.Pointer, b.Bytes, b.Start)).Order());
    }

    private sealed class Recording
    {
        private const ulong ModuleBase = 0x7FF0_0000_0000;

        private CallTree Tree { get; } = new();

        private List<CallTreeNode?> Nodes { get; } = [];

        private Dictionary<string, (CallFrame Frame, List<ulong[]> Rows)> Functions { get; } = new();

        public MemoryObjectMap Build(IReadOnlyDictionary<ulong, MemoryObjectInfo> types)
        {
            var builder = new TraceCallStore.Builder();

            foreach (var (frame, rows) in Functions.Values)
            {
                builder.Add(frame.Address, frame.InstanceAddress, TraceCallRowHelper.Chunk([.. rows]), rows.Count);
            }

            return MemoryObjectMap.Build(builder.Build(), Nodes, types);
        }

        public Recording Call(string className, string method, ulong sequence, ulong rcx, ulong rdx = 0, ulong returned = 0)
        {
            var key = $"{className}::{method}";

            if (!Functions.TryGetValue(key, out var function))
            {
                var rva = (uint)(0x1000 + Functions.Count * 0x100);

                var frame = new CallFrame
                {
                    Module = new("sqldk"),
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

                Functions[key] = function;
            }

            function.Rows.Add(TraceCallRowHelper.Row(sequence,
                                                flags: returned != 0 ? TraceCallChunk.ReturnedFlag : 0,
                                                rcx: rcx,
                                                rdx: rdx,
                                                returnValue: returned,
                                                callNodeIndex: Nodes.Count - 1));

            return this;
        }
    }
}
