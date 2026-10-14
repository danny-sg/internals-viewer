using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;

namespace InternalsViewer.Query.FullTrace.Tests;

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

        Assert.Null(map.GetHoBtId(Access, 9));
        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access, 10));
        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access, 50));
        Assert.Null(map.GetHoBtId(Access, 51));
    }

    [Fact]
    public void A_Packed_HoBtId_Is_Unpacked()
    {
        var map = new Recording().Init(Access, ClusteredColumnstore >> 16, sequence: 10).Build();

        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access, 20));
    }

    [Fact]
    public void A_Full_Id_Passed_On_As_A_Packed_HoBtId_Is_Taken_Whole_Even_With_Its_Top_Bits_Clear()
    {
        const ulong worktable = 0x0000_8000_0016_0000;

        var map = new Recording().Init(Access, worktable, sequence: 10).Init(Access, worktable >> 16, sequence: 10).Build();

        Assert.Equal(worktable, map.GetHoBtId(Access, 10));
    }

    [Fact]
    public void A_Reused_Address_Takes_The_HoBt_Of_Its_Latest_Init()
    {
        var map = new Recording().Init(Access, ClusteredColumnstore, sequence: 10)
                                 .Destroy(Access, sequence: 20)
                                 .Init(Access, DeleteBitmap, sequence: 30)
                                 .Build();

        Assert.Equal(ClusteredColumnstore, map.GetHoBtId(Access, 15));
        Assert.Null(map.GetHoBtId(Access, 25));
        Assert.Equal(DeleteBitmap, map.GetHoBtId(Access, 35));
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

        var layout = KnownParameters.Apply(ArgumentLayout.LayoutOf(signature), "HoBtAccess", "Init");

        Assert.Equal(["this", string.Empty, "hobt_id", string.Empty], layout.Slots.Select(s => s.Name));
        Assert.Equal([false, false, true, false], layout.Slots.Select(s => s.IsHoBtId));
    }

    [Fact]
    public void Other_Functions_And_The_HoBtId_Overload_Are_Left_Alone()
    {
        var packed = new FunctionSignature(["enum ACCESSTYPE", "HoBtId", "XDES *", "CreateHoBtInfo *"], FunctionType.Member, "void");

        var other = new FunctionSignature(["unsigned __int64"], FunctionType.Member, "void");

        Assert.DoesNotContain(KnownParameters.Apply(ArgumentLayout.LayoutOf(packed), "HoBtAccess", "Init").Slots, s => s.IsHoBtId);
        Assert.DoesNotContain(KnownParameters.Apply(ArgumentLayout.LayoutOf(other), "HoBtAccess", "GetDbId").Slots, s => s.IsHoBtId);
    }

    private sealed class Recording
    {
        private const ulong ModuleBase = 0x7FF0_0000_0000;

        private CallStackTree Tree { get; } = new();

        private List<CallStackNode?> Nodes { get; } = [];

        private Dictionary<string, (CallstackFrame Frame, List<ulong[]> Rows)> Functions { get; } = new();

        public Recording Init(ulong access, ulong hobtId, ulong sequence) => Call("Init", sequence, access, 0, hobtId);

        public Recording Destroy(ulong access, ulong sequence) => Call("~HoBtAccess", sequence, access, 0, 0);

        public HoBtAccessMap Build()
        {
            var builder = new TraceCallLog.Builder();

            foreach (var (frame, rows) in Functions.Values)
            {
                builder.Add(frame.Address, frame.Instance, Columns(rows), rows.Count);
            }

            return HoBtAccessMap.Build(builder.Build(), Nodes);
        }

        private Recording Call(string method, ulong sequence, ulong rcx, ulong rdx, ulong r8)
        {
            if (!Functions.TryGetValue(method, out var function))
            {
                var rva = (uint)(0x1000 + Functions.Count * 0x100);

                var frame = new CallstackFrame
                {
                    Module = "sqlmin",
                    Rva = rva,
                    Address = ModuleBase + rva,
                    Resolved = new ResolvedCallstackFrame
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

            function.Rows.Add([sequence, 1, rcx, rdx, r8, 0, 0, (ulong)(Nodes.Count - 1)]);

            return this;
        }

        private static ulong[] Columns(List<ulong[]> rows)
        {
            var columns = new ulong[rows.Count * TraceArgumentCall.ValueCount];

            for (var call = 0; call < rows.Count; call++)
            {
                for (var value = 0; value < rows[call].Length; value++)
                {
                    columns[value * rows.Count + call] = rows[call][value];
                }
            }

            return columns;
        }
    }
}
