using System.Text;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.Query.FullTrace.Native;
using InternalsViewer.Query.FullTrace.Timeline;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class ObjectMapTests
{
    private const ulong Scan = 0x1000;

    private const ulong ScanConstructor = 0x1100;

    private const ulong ScanDestructor = 0x1200;

    private const ulong BaseScan = 0x2000;

    private const ulong BaseConstructor = 0x2100;

    private const ulong Seek = 0x3000;

    private const ulong Caller = 0x8000;

    private const ulong OperatorNew = 0x8100;

    private const ulong Allocator = 0x8200;

    private const ulong Free = 0x8300;

    private const ulong Pass = 0x9000;

    private const ulong Address = 0x100A0;

    private const ulong ReturnedFlag = 1ul << 32;

    private static readonly ObjectFunctions Functions = new(new Dictionary<ulong, TraceMember>
                                                            {
                                                                [Scan] = new("CQScanRangeNew", TraceMemberKind.Method),
                                                                [ScanConstructor] = new("CQScanRangeNew", TraceMemberKind.Constructor),
                                                                [ScanDestructor] = new("CQScanRangeNew", TraceMemberKind.Destructor),
                                                                [BaseScan] = new("CQScanNew", TraceMemberKind.Method),
                                                                [BaseConstructor] = new("CQScanNew", TraceMemberKind.Constructor),
                                                                [Seek] = new("SeekKey", TraceMemberKind.Method)
                                                            },
                                                            new HashSet<ulong> { Allocator },
                                                            new Dictionary<ulong, int> { [Free] = 0 });

    [Fact]
    public void An_Address_Holds_The_Object_Its_Member_Calls_Were_Made_On()
    {
        var map = Build(new Function(Scan, [new(Address, 10, 11), new(Address, 12, 13)]));

        Assert.Equal("CQScanRangeNew", map.GetObject(Address, 12)?.ClassName);
    }

    [Fact]
    public void A_Later_Object_Of_Another_Class_At_The_Same_Address_Is_A_New_Object()
    {
        var map = Build(new Function(Scan, [new(Address, 10, 11), new(Address, 12, 13)]), new(Seek, [new(Address, 100, 101)]));

        Assert.Equal(("CQScanRangeNew", "SeekKey"), (map.GetObject(Address, 12)?.ClassName, map.GetObject(Address, 100)?.ClassName));
    }

    [Fact]
    public void Base_Class_Calls_During_An_Object_Belong_To_It()
    {
        var map = Build(new Function(Scan, [new(Address, 10, 11), new(Address, 20, 21)]), new(BaseScan, [new(Address, 15, 16)]));

        Assert.Equal((1, "CQScanRangeNew"), (map.Count, map.GetObject(Address, 15)?.ClassName));
    }

    [Fact]
    public void An_Address_Between_Its_Objects_Holds_Nothing_Known()
    {
        var map = Build(new Function(Scan, [new(Address, 10, 11), new(Address, 12, 13)]), new(Seek, [new(Address, 100, 101)]));

        Assert.Null(map.GetObject(Address, 50));
    }

    [Fact]
    public void A_Small_Value_Is_Never_An_Object()
    {
        Assert.Null(Build(new Function(Scan, [new(0x1, 10, 11)])).GetObject(0x1, 10));
    }

    [Fact]
    public void A_Function_That_Is_Not_A_Member_Places_Nothing()
    {
        var map = Build(new Function(Pass, [new(Address, 10, 11)]));

        Assert.Null(map.GetObject(Address, 10));
    }

    [Fact]
    public void An_Object_Destroyed_And_Constructed_Again_At_The_Same_Address_Is_A_New_Object()
    {
        var map = Build(new Function(ScanConstructor, [new(Address, 10, 11), new(Address, 30, 31)]),
                        new(Scan, [new(Address, 12, 13), new(Address, 32, 33)]),
                        new(ScanDestructor, [new(Address, 20, 21)]));

        Assert.NotEqual(map.GetObject(Address, 12), map.GetObject(Address, 32));
    }

    [Fact]
    public void An_Address_Holds_Nothing_Known_After_Its_Object_Is_Destroyed()
    {
        var map = Build(new Function(ScanConstructor, [new(Address, 10, 11), new(Address, 30, 31)]),
                        new(Scan, [new(Address, 12, 13)]),
                        new(ScanDestructor, [new(Address, 20, 21)]));

        Assert.Null(map.GetObject(Address, 25));
    }

    [Fact]
    public void Base_Class_Constructors_Inside_A_Constructor_Build_The_Same_Object()
    {
        var map = Build(new Function(ScanConstructor, [new(Address, 10, 20)]),
                        new(BaseConstructor, [new(Address, 11, 12)]),
                        new(Scan, [new(Address, 30, 31)]));

        Assert.Equal((1, "CQScanRangeNew"), (map.Count, map.GetObject(Address, 11)?.ClassName));
    }

    [Fact]
    public void Calls_Inside_A_Destructor_Belong_To_The_Object_Being_Destroyed()
    {
        var map = Build(new Function(Scan, [new(Address, 12, 13), new(Address, 41, 42)]), new(ScanDestructor, [new(Address, 40, 50)]));

        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void A_Call_After_A_Destructor_Is_On_A_New_Object()
    {
        var map = Build(new Function(Scan, [new(Address, 10, 11), new(Address, 30, 31)]), new(ScanDestructor, [new(Address, 20, 21)]));

        Assert.NotEqual(map.GetObject(Address, 10), map.GetObject(Address, 30));
    }

    [Fact]
    public void A_Constructor_On_An_Object_In_Use_Builds_A_New_Object()
    {
        var map = Build(new Function(Scan, [new(Address, 10, 11), new(Address, 30, 31)]), new(ScanConstructor, [new(Address, 20, 21)]));

        Assert.NotEqual(map.GetObject(Address, 10), map.GetObject(Address, 30));
    }

    [Fact]
    public void A_Value_Is_Looked_Up_At_The_Position_Of_The_Call_That_Passes_It()
    {
        var (log, timeline) = Trace(new Function(ScanConstructor, [new(Address, 10, 10, 1, 2), new(Address, 10, 10, 6, 8)]),
                                    new(ScanDestructor, [new(Address, 10, 10, 3, 4)]),
                                    new(Pass, [new(Address, 10, 10, 5, 5), new(Address, 10, 10, 7, 7)]));

        var map = ObjectMap.Build(log, timeline, Functions);

        var passes = log.CallsOf(Pass, 0)!;

        Assert.Equal((null, "CQScanRangeNew"), (map.GetObject(Address, passes[0])?.ClassName, map.GetObject(Address, passes[1])?.ClassName));
    }

    [Fact]
    public void A_Call_Missing_From_The_Timeline_Places_Its_Object_At_Its_Sequence()
    {
        var map = Build(new Function(Scan, [new(Address, 10, 11, IsSpanned: false)]));

        Assert.Equal("CQScanRangeNew", map.GetObject(Address, 10)?.ClassName);
    }

    [Fact]
    public void An_Object_Starts_At_The_Outermost_Call_Returning_The_Memory_It_Was_Built_In()
    {
        var map = Build(new Function(Caller, [new(0, 0, 100)], Thread: 1),
                        new(OperatorNew, [new(0, 10, 20, Returned: Address)], Parent: 0, Thread: 1),
                        new(Allocator, [new(0, 11, 19, Returned: Address)], Parent: 1, Thread: 1),
                        new(ScanConstructor, [new(Address, 30, 31)], Parent: 0, Thread: 1));

        Assert.Equal("CQScanRangeNew", map.GetObject(Address, 10)?.ClassName);
    }

    [Fact]
    public void An_Allocation_Is_Not_Linked_To_An_Object_Built_After_Its_Caller_Returns()
    {
        var map = Build(new Function(Caller, [new(0, 0, 25)], Thread: 1),
                        new(Allocator, [new(0, 10, 20, Returned: Address)], Parent: 0, Thread: 1),
                        new(ScanConstructor, [new(Address, 30, 31)], Thread: 1));

        Assert.Null(map.GetObject(Address, 10));
    }

    [Fact]
    public void Memory_Is_Named_After_The_Outermost_Call_That_Allocated_It()
    {
        var map = Build(new Function(Caller, [new(0, 0, 100)], Thread: 1),
                        new(OperatorNew, [new(0, 10, 20, Returned: Address)], Parent: 0, Thread: 1, Name: "OperatorNew"),
                        new(Allocator, [new(0, 11, 19, Returned: Address)], Parent: 1, Thread: 1, Name: "operator new[]"),
                        new(Pass, [new(Address, 30, 31)], Parent: 0, Thread: 1));

        Assert.Equal("OperatorNew", map.Allocations.GetAllocation(Address, 30)?.Allocator);
    }

    [Fact]
    public void Memory_Is_Not_Named_While_It_Is_Being_Allocated()
    {
        var map = Build(new Function(Caller, [new(0, 0, 100)], Thread: 1),
                        new(Allocator, [new(0, 10, 20, Returned: Address)], Parent: 0, Thread: 1, Name: "operator new[]"));

        Assert.Null(map.Allocations.GetAllocation(Address, 15));
    }

    [Fact]
    public void Freed_Memory_Is_No_Longer_Named()
    {
        var map = Build(new Function(Caller, [new(0, 0, 100)], Thread: 1),
                        new(Allocator, [new(0, 10, 20, Returned: Address)], Parent: 0, Thread: 1, Name: "operator new[]"),
                        new(Free, [new(Address, 40, 41)], Parent: 0, Thread: 1, Name: "operator delete[]"));

        Assert.Equal(("operator new[]", null), (map.Allocations.GetAllocation(Address, 40)?.Allocator,
                                                map.Allocations.GetAllocation(Address, 50)?.Allocator));
    }

    [Fact]
    public void The_Map_Is_Rebuilt_From_What_Is_Written()
    {
        var (log, timeline) = Trace(new Function(Scan, [new(Address, 10, 11)]));

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            ObjectMap.Build(log, timeline, Functions).Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.Equal("CQScanRangeNew", ObjectMap.Read(reader, log, timeline).GetObject(Address, 10)?.ClassName);
    }

    private static ObjectMap Build(params Function[] functions)
    {
        var (log, timeline) = Trace(functions);

        return ObjectMap.Build(log, timeline, Functions);
    }

    private static (TraceCallLog Log, TraceTimeline Timeline) Trace(params Function[] functions)
    {
        var builder = new TraceCallLog.Builder();

        var timelineBuilder = new TraceTimeline.Builder();

        var tree = new CallStackTree();

        var nodes = new List<CallNode>();

        var mapped = new List<CallStackNode>();

        foreach (var function in functions)
        {
            var node = nodes.Count;

            var thread = function.Thread != 0 ? function.Thread : (uint)node + 100;

            var frame = new CallstackFrame
            {
                Module = "sqlmin",
                Rva = (uint)function.Address,
                Address = function.Address,
                Resolved = function.Name is { } name ? new ResolvedCallstackFrame { MethodName = name } : null
            };

            nodes.Add(new CallNode(function.Parent, 0, function.Address, 0, (ulong)function.Calls.Length));

            mapped.Add(tree.AddCall(function.Parent < 0 ? tree.Root : mapped[function.Parent], frame, function.Calls.Length));

            builder.Add(function.Address, 0, Chunk(function.Calls, node, thread), function.Calls.Length);

            timelineBuilder.Add([.. function.Calls.Select((c, index) => (Call: c, Index: index))
                                                  .Where(c => c.Call.IsSpanned)
                                                  .Select(c => new CallSpan(c.Call.Start,
                                                                            c.Call.StartSteps,
                                                                            c.Call.End,
                                                                            c.Call.EndSteps,
                                                                            c.Call.Start,
                                                                            c.Call.End,
                                                                            node,
                                                                            thread,
                                                                            (uint)c.Index,
                                                                            0))]);
        }

        var timeline = timelineBuilder.Build([.. nodes]);

        timeline.MapNodes([.. mapped]);

        return (builder.Build(), timeline);
    }

    private static ulong[] Chunk(Call[] calls, int node, uint thread)
    {
        var columns = new ulong[calls.Length * TraceArgumentCall.ValueCount];

        for (var call = 0; call < calls.Length; call++)
        {
            columns[call] = calls[call].Start;

            columns[calls.Length + call] = thread | (calls[call].Returned != 0 ? ReturnedFlag : 0);

            columns[2 * calls.Length + call] = calls[call].Self;

            columns[6 * calls.Length + call] = calls[call].Returned;

            columns[7 * calls.Length + call] = (ulong)node;
        }

        return columns;
    }

    private sealed record Function(ulong Address, Call[] Calls, int Parent = -1, uint Thread = 0, string? Name = null);

    private readonly record struct Call(ulong Self,
                                        ulong Start,
                                        ulong End,
                                        ulong StartSteps = 0,
                                        ulong EndSteps = 0,
                                        bool IsSpanned = true,
                                        ulong Returned = 0);
}
