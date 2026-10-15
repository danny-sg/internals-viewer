using System.Text;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class MemoryMapTests
{
    private const ulong Address = 0x1F4EEAE1820;

    private const int OperatorNew = 0;

    private const int ScanConstructor = 1;

    private const int ScanMethod = 2;

    private static readonly CallStackNode[] Nodes = GetNodes();

    [Fact]
    public void An_Address_Holds_What_Its_Latest_Entry_Says()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 10, ScanMethod),
                        Entry(MemoryMapOperation.Allocated, call: 20, OperatorNew));

        Assert.Equal(("CQScanRangeNew", "OperatorNew"), (map.GetEntry(Address, 15)?.ClassName, map.GetEntry(Address, 25)?.Allocator));
    }

    [Fact]
    public void An_Allocation_Takes_The_Class_Of_The_Object_Built_In_It()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew),
                        Entry(MemoryMapOperation.Visited, call: 20, ScanMethod));

        Assert.Equal(("CQScanRangeNew", "OperatorNew"), (map.GetEntry(Address, 15)?.ClassName, map.GetEntry(Address, 25)?.Allocator));
    }

    [Fact]
    public void An_Allocation_With_No_Object_Built_In_It_Has_No_Class()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew),
                        Entry(MemoryMapOperation.Allocated, call: 20, OperatorNew));

        Assert.Equal(((string?)null, "OperatorNew"), (map.GetEntry(Address, 15)?.ClassName, map.GetEntry(Address, 15)?.Allocator));
    }

    [Fact]
    public void An_Address_Holds_Nothing_Before_It_Is_Allocated()
    {
        var map = Build(Entry(MemoryMapOperation.Allocated, call: 10, OperatorNew));

        Assert.Null(map.GetEntry(Address, 5));
    }

    [Fact]
    public void A_Stack_Address_Holds_Nothing_After_Its_Frame_Returns()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 10, ScanMethod, owner: 5, until: 30));

        Assert.Equal(("CQScanRangeNew", null), (map.GetEntry(Address, 30)?.ClassName, map.GetEntry(Address, 31)?.ClassName));
    }

    [Fact]
    public void Memory_Visited_First_Held_Its_Object_Before_The_Visit()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 50, ScanMethod));

        Assert.Equal("CQScanRangeNew", map.GetEntry(Address, 10)?.ClassName);
    }

    [Fact]
    public void A_Stack_Address_Visited_First_Holds_Nothing_Before_The_Visit()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 50, ScanMethod, owner: 40, until: 60));

        Assert.Null(map.GetEntry(Address, 10));
    }

    [Fact]
    public void A_Call_Without_An_Id_Finds_Nothing()
    {
        var map = Build(Entry(MemoryMapOperation.Visited, call: 50, ScanMethod));

        Assert.Null(map.GetEntry(Address, 0));
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

        Assert.Equal((2, "OperatorNew"), (read.Count, read.GetEntry(Address, 15)?.Allocator));
    }

    private static MemoryMap Build(params AddressEntry[] entries) => MemoryMap.Build(entries, NodeOf);

    private static AddressEntry Entry(MemoryMapOperation operation, ulong call, int node, ulong owner = 0, ulong until = 0)
        => new(Address, call, owner, until, node, 7, operation);

    private static CallStackNode? NodeOf(int node) => node >= 0 && node < Nodes.Length ? Nodes[node] : null;

    private static CallStackNode[] GetNodes()
    {
        var tree = new CallStackTree();

        return [tree.AddCall(tree.Root, Frame(0x10, null, "OperatorNew"), 1),
                tree.AddCall(tree.Root, Frame(0x20, "CQScanRangeNew", "CQScanRangeNew"), 1),
                tree.AddCall(tree.Root, Frame(0x30, "CQScanRangeNew", "SetRange"), 1)];
    }

    private static CallstackFrame Frame(uint rva, string? className, string methodName) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Address = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = className, MethodName = methodName }
    };
}
