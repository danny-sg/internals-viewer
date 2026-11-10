using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Symbols;
using InternalsViewer.Query.FullTrace.Symbols.Enums;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Tests.Memory.MemoryObject;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Arguments;

[Trait("Category", "Unit")]
public class HoBtAccessMapTests
{
    private const ulong Access = 0x204_FE71_6550;

    private const ulong ClusteredColumnstore = 72057594047299584;

    private const ulong DeleteBitmap = 72057594047365120;

    [Fact]
    public void An_Access_Object_Takes_The_HoBt_It_Was_Initialised_With_Until_It_Is_Destroyed()
    {
        var map = new Recording().Init(Access, ClusteredColumnstore, sequence: 10).Destroy(Access, sequence: 50).Build();

        Assert.Null(map.GetHoBtId(Access, At(9)));
        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access, At(10)));
        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access, At(50)));
        Assert.Null(map.GetHoBtId(Access, At(51)));
    }

    [Fact]
    public void A_Packed_HoBtId_Is_Unpacked()
    {
        var map = new Recording().Init(Access, ClusteredColumnstore >> 16, sequence: 10).Build();

        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access, At(20)));
    }

    [Fact]
    public void A_Full_Id_Passed_On_As_A_Packed_HoBtId_Is_Taken_Whole_Even_With_Its_Top_Bits_Clear()
    {
        const ulong worktable = 0x0000_8000_0016_0000;

        var map = new Recording().Init(Access, worktable, sequence: 10).Init(Access, worktable >> 16, sequence: 10).Build();

        Assert.Equal(worktable, map.GetHoBtId(Access, At(10)));
    }

    [Fact]
    public void A_Reused_Address_Takes_The_HoBt_Of_Its_Latest_Init()
    {
        var map = new Recording().Init(Access, ClusteredColumnstore, sequence: 10)
                                 .Destroy(Access, sequence: 20)
                                 .Init(Access, DeleteBitmap, sequence: 30)
                                 .Build();

        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access, At(15)));
        Assert.Null(map.GetHoBtId(Access, At(25)));
        Assert.Equal(DeleteBitmap, map.GetHoBtId(Access, At(35)));
        Assert.Null(map.GetHoBtId(Access));
    }

    [Fact]
    public void An_Address_Only_Ever_Used_For_One_HoBt_Is_Identified_Without_A_Position()
    {
        var map = new Recording().Init(Access, ClusteredColumnstore, sequence: 10)
                                 .Destroy(Access, sequence: 20)
                                 .Init(Access, ClusteredColumnstore, sequence: 30)
                                 .Build();

        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access));
        Assert.Null(map.GetHoBtId(0x1234));
    }

    [Fact]
    public void The_Full_HoBt_Id_Parameter_Of_Init_Is_Named_And_Marked()
    {
        var signature = new FunctionSignature(["enum ACCESSTYPE", "unsigned __int64", "XDES *"], FunctionType.Member, "void");

        var layout = KnownParameterLookup.Apply(ArgumentLayout.GetLayout(signature), "HoBtAccess", "Init");

        Assert.Equal(["this", string.Empty, "hobt_id", string.Empty], layout.Slots.Select(s => s.Name));
        Assert.Equal([false, false, true, false], layout.Slots.Select(s => s.IsHoBtId));
    }

    [Fact]
    public void The_HoBt_Id_Of_A_Free_Function_Is_Marked_And_Other_Integers_Are_Not()
    {
        var signature = new FunctionSignature(["XDES *", "unsigned __int64", "HoBtAccess &", "LCK_MODE &", "unsigned __int64 &"],
                                              FunctionType.Free,
                                              "long");

        var layout = KnownParameterLookup.Apply(ArgumentLayout.GetLayout(signature), null, "GetHoBtLockInternal");

        Assert.Equal([false, true, false, false, false], layout.Slots.Select(s => s.IsHoBtId));
    }

    [Fact]
    public void Only_The_Listed_Parameter_Is_Marked_When_A_Function_Takes_Several_Integers()
    {
        var signature = new FunctionSignature(["IMemObj *",
                                               "unsigned __int64",
                                               "XDES *",
                                               "unsigned long",
                                               "enum AccessorFlagEx",
                                               "unsigned __int64"],
                                              FunctionType.Free,
                                              "long");

        var layout = KnownParameterLookup.Apply(ArgumentLayout.GetLayout(signature), null, "CreateAccessorCommon");

        Assert.Equal([1], layout.Slots.Select((s, i) => (s, i)).Where(s => s.s.IsHoBtId).Select(s => s.i));
    }

    [Fact]
    public void The_HoBt_Id_Of_A_Member_Function_Is_Counted_After_This()
    {
        var signature = new FunctionSignature(["unsigned __int64", "XDES *", "RowsetExecContext const *"], FunctionType.Member, "long");

        var layout = KnownParameterLookup.Apply(ArgumentLayout.GetLayout(signature), "HeapDataSetSession", "WakeUpInternal");

        Assert.Equal(["this", "hobt_id", string.Empty, string.Empty], layout.Slots.Select(s => s.Name));
    }

    [Fact]
    public void Other_Functions_And_The_HoBtId_Overload_Are_Left_Alone()
    {
        var packed = new FunctionSignature(["enum ACCESSTYPE", "HoBtId", "XDES *", "CreateHoBtInfo *"], FunctionType.Member, "void");

        var other = new FunctionSignature(["unsigned __int64"], FunctionType.Member, "void");

        Assert.DoesNotContain(KnownParameterLookup.Apply(ArgumentLayout.GetLayout(packed), "HoBtAccess", "Init").Slots, s => s.IsHoBtId);
        Assert.DoesNotContain(KnownParameterLookup.Apply(ArgumentLayout.GetLayout(other), "HoBtAccess", "GetDbId").Slots, s => s.IsHoBtId);
    }

    private sealed class Recording
    {
        private const ulong ModuleBase = 0x7FF0_0000_0000;

        private CallTree Tree { get; } = new();

        private List<CallTreeNode?> Nodes { get; } = [];

        private Dictionary<string, (CallFrame Frame, List<ulong[]> Rows)> Functions { get; } = new();

        public Recording Init(ulong access, ulong hobtId, ulong sequence) => Call("Init", sequence, access, 0, hobtId);

        public Recording Destroy(ulong access, ulong sequence) => Call("~HoBtAccess", sequence, access, 0, 0);

        public HoBtAccessMap Build()
        {
            var builder = new TraceCallStore.Builder();

            foreach (var (frame, rows) in Functions.Values)
            {
                builder.Add(frame.Address, frame.InstanceAddress, TraceCallRowHelper.Chunk([.. rows]), rows.Count);
            }

            return HoBtAccessMap.Build(builder.Build(), Nodes);
        }

        private Recording Call(string method, ulong sequence, ulong rcx, ulong rdx, ulong r8)
        {
            if (!Functions.TryGetValue(method, out var function))
            {
                var rva = (uint)(0x1000 + Functions.Count * 0x100);

                var frame = new CallFrame
                {
                    Module = new("sqlmin"),
                    Rva = rva,
                    Address = ModuleBase + rva,
                    Function = new FrameFunction
                    {
                        ClassName = "HoBtAccess",
                        MethodName = method,
                        SymbolCategory = SymbolCategory.Unknown
                    }
                };

                Nodes.Add(Tree.AddCall(Tree.Root, frame, 1));

                function = (frame, []);

                Functions[method] = function;
            }

            function.Rows.Add(TraceCallRowHelper.Row(sequence, flags: 1, rcx: rcx, rdx: rdx, r8: r8, callNodeIndex: Nodes.Count - 1));

            return this;
        }
    }
}
