using System.Buffers.Binary;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.Memory.History.MemoryHistoryStores;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class ArrayAllocationMatcherTests
{
    private const ulong Columns = 0x21C_0000_1000;

    private const ulong Buffer = 0x21C_0000_2000;

    private const ulong Instance = 0x21C_0000_3000;

    private const int ArrayNew = 0;

    private const int OperatorNew = 1;

    private const int ColumnConstructor = 2;

    private const int ColumnBytes = 0x160;

    private static readonly CallStackNode[] Nodes = GetNodes();

    [Fact]
    public void An_Array_Is_Named_By_Its_Element_And_Its_Count()
    {
        var matcher = GetMatcher(Columns, ArrayNew, 8, 8 + 8 * ColumnBytes);

        Assert.Equal("CSIScanBucketColumn[8]", matcher.GetClassName(0));
    }

    [Fact]
    public void A_Count_That_Does_Not_Divide_The_Block_Is_Not_An_Array()
        => Assert.Null(GetMatcher(Buffer, ArrayNew, 7, 8 + 8 * ColumnBytes).GetClassName(0));

    [Fact]
    public void An_Object_Built_By_Operator_New_Is_Not_An_Array()
        => Assert.Null(GetMatcher(Instance, OperatorNew, 8, 8 + 8 * ColumnBytes).GetClassName(0));

    private static ArrayAllocationMatcher GetMatcher(ulong address, int allocator, ulong count, int size)
    {
        var header = new byte[sizeof(ulong)];

        BinaryPrimitives.WriteUInt64LittleEndian(header, count);

        var store = Build([Block(address, (ulong)size)], [(2, address, header)]);

        var map = MemoryMap.Build([Entry(MemoryMapOperation.Allocated, address, 1, allocator),
                                   Entry(MemoryMapOperation.Constructed, address + sizeof(ulong), 2, ColumnConstructor)],
                                  i => i >= 0 && i < Nodes.Length ? Nodes[i] : null);

        return new ArrayAllocationMatcher(store, map);
    }

    private static AddressEntry Entry(MemoryMapOperation operation, ulong address, ulong call, int callNodeIndex)
        => new(address, call, 0, 0, At(call), TracePosition.Unknown, callNodeIndex, 9, operation);

    private static CallStackNode[] GetNodes()
    {
        var tree = new CallStackTree();

        return [tree.AddCall(tree.Root, Frame(0x10, null, "operator new[]"), 1),
                tree.AddCall(tree.Root, Frame(0x20, null, "operator new"), 1),
                tree.AddCall(tree.Root, Frame(0x30, "CSIScanBucketColumn", "CSIScanBucketColumn"), 1)];
    }

    private static CallstackFrame Frame(uint rva, string? className, string methodName) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Address = rva,
        Resolved = new ResolvedCallstackFrame { ClassName = className, MethodName = methodName }
    };
}
