using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory;

[Trait("Category", "Unit")]
public class InstanceMemoryCurvesTests
{
    private const uint Thread = 7;

    private const ulong QueryScan = 0x1F4_0000_1000;

    private const ulong HashMatch = 0x1F4_0000_2000;

    private const ulong PlanObject = 0x1F4_0000_3000;

    [Fact]
    public void An_Object_Keeps_Memory_It_Allocated_On_Its_Later_Calls()
    {
        var (curves, nodes) = Build();

        var curve = curves.GetCurve(Thread, nodes.Hash);

        Assert.Equal((100ul, 100ul), (curve!.PeakDuring(At(12), At(20)), curve.PeakDuring(At(30), At(40))));
    }

    [Fact]
    public void Memory_Belongs_To_The_Innermost_Object_Whose_Call_Made_It()
    {
        var (curves, nodes) = Build();

        Assert.Equal(0ul, curves.GetCurve(Thread, nodes.Query)!.PeakDuring(At(0), At(50)));
    }

    [Fact]
    public void Memory_Made_Under_A_Plan_Object_Belongs_To_The_Object_Around_It()
    {
        var (curves, nodes) = Build();

        Assert.Equal(50ul, curves.GetCurve(Thread, nodes.Query)!.PeakDuring(At(52), At(100)));
    }

    [Fact]
    public void Calls_That_Are_Not_On_An_Object_Have_No_Object_Curve()
    {
        var (curves, nodes) = Build();

        Assert.Null(curves.GetCurve(Thread, nodes.Allocator));
    }

    [Fact]
    public void Plan_Objects_Do_Not_Own_Memory()
    {
        var (curves, nodes) = Build();

        Assert.Null(curves.GetCurve(Thread, nodes.Plan));
    }

    private static (InstanceMemoryCurves Curves, Nodes Nodes) Build()
    {
        var tree = new CallStackTree();

        var query = tree.AddCall(tree.Root, Frame("CQueryScan", "GetRow", 0x10, QueryScan), 1);
        var hash = tree.AddCall(query, Frame("CQScanHash", "GetRow", 0x20, HashMatch), 2);
        var allocator = tree.AddCall(hash, Frame(string.Empty, "operator new", 0x30), 1);
        var plan = tree.AddCall(query, Frame("CXteHash", "QScanGet", 0x40, PlanObject), 1);

        var builder = new TraceTimeline.Builder();

        builder.Add([Span(0, 0, 100, 0), Span(1, 10, 20, 0), Span(2, 12, 13, 0), Span(1, 30, 40, 1), Span(3, 50, 60, 0)]);

        var timeline = builder.Build([new(-1, 0, 0x10, QueryScan, 1),
                                      new(0, 0, 0x20, HashMatch, 2),
                                      new(1, 0, 0x30, 0, 1),
                                      new(0, 0, 0x40, PlanObject, 1)]);

        timeline.MapNodes([query, hash, allocator, plan]);

        var memory = MemoryIndex.Build([new(Thread, At(12), At(13), 100, 0xA), new(Thread, At(52), At(53), 50, 0xB)], []);

        return (InstanceMemoryCurves.Build(timeline, memory), new Nodes(query, hash, allocator, plan));
    }

    private static CallstackFrame Frame(string className, string method, uint rva, ulong instance = 0) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Address = rva,
        InstanceAddress = instance,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = SymbolCategory.Unknown
        }
    };

    private static CallSpan Span(int callNodeIndex, ulong start, ulong end, uint callIndex)
        => new(start, 0, end, 0, start, end, callNodeIndex, Thread, callIndex, 1);

    private sealed record Nodes(CallStackNode Query, CallStackNode Hash, CallStackNode Allocator, CallStackNode Plan);
}
