using System.Buffers.Binary;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.Allocation.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using static InternalsViewer.Query.FullTrace.Tests.Memory.History.MemoryHistoryStoreHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.History;

[Trait("Category", "Unit")]
public class MemoryHistoryArrayTests
{
    private const ulong Address = 0x21C_0000_1000;

    private const int ElementBytes = 0x160;

    [Fact]
    public void Operator_New_Array_Is_Its_Own_Allocation_Operation()
    {
        Assert.Equal((AllocationOperation.AllocateArray, AllocationOperation.Allocate),
                     (AllocationFunction.Classify(0, "operator new[]")!.Operation,
                      AllocationFunction.Classify(0, "operator new")!.Operation));
    }

    [Fact]
    public void An_Array_Block_Has_Its_Count_And_Its_First_Element_After_The_Count()
    {
        Assert.Equal((Address + sizeof(ulong), 8u), GetStore(count: 8, isArray: true).GetArray(0));
    }

    [Fact]
    public void A_Count_That_Does_Not_Divide_The_Block_Is_Not_An_Array()
    {
        Assert.Null(GetStore(count: 7, isArray: true).GetArray(0));
    }

    [Fact]
    public void A_Block_Allocated_Without_Operator_New_Array_Is_Not_An_Array()
    {
        Assert.Null(GetStore(count: 8, isArray: false).GetArray(0));
    }

    private static MemoryHistoryStore GetStore(ulong count, bool isArray)
    {
        var header = new byte[sizeof(ulong)];

        BinaryPrimitives.WriteUInt64LittleEndian(header, count);

        return Build([Block(Address, sizeof(ulong) + 8 * ElementBytes)], [(2, Address, header)], isArray: isArray);
    }
}
