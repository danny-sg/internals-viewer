using System.Text;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryMapTests
{
    private const ulong Address = 0x1F4EEAE1820;

    private const int OperatorNew = 0;

    private const int ScanConstructor = 1;

    private const int ScanMethod = 2;

    private const int Factory = 3;

    private static readonly CallStackNode[] Nodes = GetNodes();

    [Fact]
    public void An_Address_Holds_What_Its_Latest_Entry_Says()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 10, ScanMethod),
                        Entry(MemoryMapOperation.Allocated, call: 20, OperatorNew));

        Assert.Equal(("CQScanRangeNew", "OperatorNew"),
                     (map.GetEntry(Address, At(15))?.ClassName, map.GetEntry(Address, At(25))?.Allocator));
    }

    [Fact]
    public void An_Allocation_Takes_The_Class_Of_The_Object_Built_In_It()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew),
                        Entry(MemoryMapOperation.Visited, call: 20, ScanMethod));

        Assert.Equal(("CQScanRangeNew", "OperatorNew"),
                     (map.GetEntry(Address, At(15))?.ClassName, map.GetEntry(Address, At(25))?.Allocator));
    }

    [Fact]
    public void An_Allocation_With_No_Object_Built_In_It_Has_No_Class()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew),
                        Entry(MemoryMapOperation.Allocated, call: 20, OperatorNew));

        Assert.Equal(((string?)null, "OperatorNew"), (map.GetEntry(Address, At(15))?.ClassName, map.GetEntry(Address, At(15))?.Allocator));
    }

    [Fact]
    public void An_Address_Holds_Nothing_Before_It_Is_Allocated()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew));

        Assert.Null(map.GetEntry(Address, At(5)));
    }

    [Fact]
    public void A_Stack_Address_Holds_Nothing_After_Its_Frame_Returns()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 10, ScanMethod, owner: 5, until: 30));

        Assert.Equal(("CQScanRangeNew", null), (map.GetEntry(Address, At(30))?.ClassName, map.GetEntry(Address, At(31))?.ClassName));
    }

    [Fact]
    public void Memory_Visited_First_Held_Its_Object_Before_The_Visit()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 50, ScanMethod));

        Assert.Equal("CQScanRangeNew", map.GetEntry(Address, At(10))?.ClassName);
    }

    [Fact]
    public void A_Stack_Address_Visited_First_Holds_Nothing_Before_The_Visit()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 50, ScanMethod, owner: 40, until: 60));

        Assert.Null(map.GetEntry(Address, At(10)));
    }

    [Fact]
    public void An_Unknown_Moment_Finds_Nothing()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 50, ScanMethod));

        Assert.Null(map.GetEntry(Address, TracePosition.Unknown));
    }

    [Fact]
    public void The_Map_Is_Rebuilt_From_What_Is_Written()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew),
                        Entry(MemoryMapOperation.Constructed, call: 20, ScanConstructor));

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            map.Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var read = MemoryMap.Read(reader, NodeOf);

        Assert.Equal((2, "OperatorNew"), (read.Count, read.GetEntry(Address, At(15))?.Allocator));
    }

    [Fact]
    public void A_Declared_Type_Names_Memory_Nothing_Else_Has()
    {
        var map = Build(Entry(MemoryMapOperation.Declared, call: 10, Factory, className: "AccBindings"));

        Assert.Equal("AccBindings", map.GetEntry(Address, At(15))?.ClassName);
    }

    [Fact]
    public void A_Declared_Type_Takes_The_Class_Of_The_Member_Call_That_Follows()
    {
        var map = Build(Entry(MemoryMapOperation.Declared, call: 10, Factory, className: "BaseXact"),
                        Entry(MemoryMapOperation.Visited, call: 20, ScanMethod));

        Assert.Equal(("CQScanRangeNew", "CQScanRangeNew"),
                     (map.GetEntry(Address, At(15))?.ClassName, map.GetEntry(Address, At(25))?.ClassName));
    }

    [Fact]
    public void A_Declared_Type_Keeps_Its_Class_When_The_Next_Object_Is_Another()
    {
        var map = Build(Entry(MemoryMapOperation.Declared, call: 10, Factory, owner: 5, until: 15, className: "BaseXact"),
                        Entry(MemoryMapOperation.Visited, call: 20, ScanMethod));

        Assert.Equal("BaseXact", map.GetEntry(Address, At(12))?.ClassName);
    }

    [Fact]
    public void An_Allocation_Takes_Its_Class_Through_A_Declared_Type()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew, bytes: 0x40),
                        Entry(MemoryMapOperation.Declared, call: 20, Factory, className: "AccBindings"));

        var declared = map.GetEntry(Address, At(25));

        Assert.Equal(("AccBindings", "OperatorNew", 0x40ul),
                     (map.GetEntry(Address, At(15))?.ClassName, declared?.Allocator, declared?.Bytes));
    }

    [Fact]
    public void Memory_Declared_First_Held_Its_Object_Before_The_Call()
    {
        var map = Build(Entry(MemoryMapOperation.Declared, call: 50, Factory, className: "AccBindings"));

        Assert.Equal("AccBindings", map.GetEntry(Address, At(10))?.ClassName);
    }

    [Fact]
    public void An_Allocation_Is_Measured_By_Its_Call()
    {
        var map = MemoryMap.Build([Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew),
                                   Entry(MemoryMapOperation.Constructed, call: 20, ScanConstructor)],
                                  NodeOf,
                                  (_, node) => node?.Symbol == "OperatorNew" ? 0x80ul : 0);

        Assert.Equal((0x80ul, 0x80ul), (map.GetEntry(Address, At(15))?.Bytes, map.GetEntry(Address, At(25))?.Bytes));
    }

    [Fact]
    public void An_Address_Inside_An_Object_Finds_The_Innermost_Object_That_Holds_It()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew, bytes: 0x100),
                        Entry(MemoryMapOperation.Constructed, call: 11, ScanConstructor),
                        Entry(MemoryMapOperation.Allocated, call: 12, OperatorNew, bytes: 0x10, address: Address + 0x20));

        var inner = map.GetContaining(Address + 0x28, At(15));

        var outer = map.GetContaining(Address + 0x80, At(15));

        Assert.Equal((Address + 0x20, 8ul), (inner?.Entry.Address, inner?.Offset));
        Assert.Equal(("CQScanRangeNew", 0x80ul), (outer?.Entry.ClassName, outer?.Offset));
        Assert.Null(map.GetContaining(Address + 0x100, At(15)));
        Assert.Null(map.GetContaining(Address + 0x80, At(5)));
    }

    [Fact]
    public void Declared_Types_And_Lengths_Are_Rebuilt_From_What_Is_Written()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew, bytes: 0x40),
                        Entry(MemoryMapOperation.Declared, call: 20, Factory, className: "AccBindings"));

        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            map.Write(writer);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        var entry = MemoryMap.Read(reader, NodeOf).GetEntry(Address, At(25));

        Assert.Equal(("OperatorNew", "AccBindings", 0x40ul), (entry?.Allocator, entry?.ClassName, entry?.Bytes));
    }

    [Fact]
    public void A_Map_Written_Before_Declared_Types_Is_Not_Current()
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(2);
        }

        stream.Position = 0;

        using var reader = new BinaryReader(stream);

        Assert.False(MemoryMap.IsCurrent(reader));
    }

    private static MemoryMap Build(params AddressEntry[] entries) => MemoryMap.Build(entries, NodeOf);

    private static AddressEntry Entry(MemoryMapOperation operation,
                                      ulong call,
                                      int callNodeIndex,
                                      ulong owner = 0,
                                      ulong until = 0,
                                      string? className = null,
                                      ulong bytes = 0,
                                      ulong address = Address)
        => new(address, call, 0, owner, At(call), until == 0 ? TracePosition.Unknown : At(until), callNodeIndex, 7, operation, className, bytes);

    private static CallStackNode? NodeOf(int callNodeIndex) => callNodeIndex >= 0 && callNodeIndex < Nodes.Length ? Nodes[callNodeIndex] : null;

    private static CallStackNode[] GetNodes()
    {
        var tree = new CallStackTree();

        return [tree.AddCall(tree.Root, Frame(0x10, null, "OperatorNew"), 1),
                tree.AddCall(tree.Root, Frame(0x20, "CQScanRangeNew", "CQScanRangeNew"), 1),
                tree.AddCall(tree.Root, Frame(0x30, "CQScanRangeNew", "SetRange"), 1),
                tree.AddCall(tree.Root, Frame(0x40, "AccessorCache", "CreateAccessor"), 1)];
    }

    private static CallstackFrame Frame(uint rva, string? className, string methodName) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Address = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = className, MethodName = methodName }
    };
}
