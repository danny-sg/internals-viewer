using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class IteratorAllocationsTests
{
    private const uint Thread = 7;

    private const ulong OpenFunction = 0x1000;

    private const ulong ConstructorFunction = 0x2000;

    private const ulong BuildFunction = 0x3000;

    private const ulong Parent = 0x1F4_0000_1000;

    private const ulong Child = 0x1F4_0000_2000;

    private const ulong PlanObject = 0x1F4_0000_3000;

    private const ulong Buffer = 0x1F4_0001_0000;

    [Fact]
    public void An_Address_Inside_An_Iterator_Object_Belongs_To_That_Iterator()
    {
        var allocations = Build([new MemoryAllocation(Thread, At(5), At(6), 0x400, Child)]);

        Assert.Equal(Child, allocations.GetOwner(Child + 0x360, At(95)));
    }

    [Fact]
    public void A_Block_Allocated_Inside_An_Iterator_Call_Belongs_To_The_Innermost_Iterator()
    {
        var allocations = Build([new MemoryAllocation(Thread, At(30), At(31), 0x100, Buffer),
                                 new MemoryAllocation(Thread, At(60), At(61), 0x100, Buffer + 0x1000)]);

        Assert.Equal((Child, Parent), (allocations.GetOwner(Buffer + 0x10, At(95)), allocations.GetOwner(Buffer + 0x1010, At(95))));
    }

    [Fact]
    public void A_Block_Allocated_While_A_Plan_Object_Builds_Its_Iterator_Belongs_To_That_Iterator()
    {
        var allocations = Build([new MemoryAllocation(Thread, At(3), At(4), 0x100, Buffer)]);

        Assert.Equal(Child, allocations.GetOwner(Buffer, At(95)));
    }

    [Fact]
    public void A_Block_Allocated_Inside_An_Iterator_Constructor_Belongs_To_That_Iterator()
    {
        var allocations = Build([new MemoryAllocation(Thread, At(110), At(111), 0x100, Buffer)]);

        Assert.Equal(Parent, allocations.GetOwner(Buffer + 0x80, At(130)));
    }

    [Fact]
    public void An_Address_In_A_Block_Freed_Before_It_Was_Read_Has_No_Owner()
    {
        var allocations = Build([new MemoryAllocation(Thread, At(30), At(31), 0x100, Buffer)],
                                [new MemoryFree(Thread, At(50), At(51), Buffer)]);

        Assert.Equal((Child, 0UL), (allocations.GetOwner(Buffer, At(45)), allocations.GetOwner(Buffer, At(60))));
    }

    [Fact]
    public void An_Address_Outside_Every_Block_Has_No_Owner()
    {
        var allocations = Build([new MemoryAllocation(Thread, At(30), At(31), 0x100, Buffer)]);

        Assert.Equal(0UL, allocations.GetOwner(Buffer + 0x100, At(95)));
    }

    private static IteratorAllocations Build(MemoryAllocation[] allocations, MemoryFree[]? frees = null)
    {
        var tree = new CallStackTree();

        CallStackNode[] mapped =
        [
            tree.AddCall(tree.Root, Frame("CQScanNLJoinNew", "Open", 0x10), 1),
            tree.AddCall(tree.Root, Frame("CQScanTableScanNew", "Open", 0x20), 1),
            tree.AddCall(tree.Root, Frame("CXteTableScan", "QScanGet", 0x30), 1),
            tree.AddCall(tree.Root, Frame("CQScanNLJoinNew", "CQScanNLJoinNew", 0x40), 1)
        ];

        var store = new TraceCallStore.Builder();

        store.Add(OpenFunction, Parent, Chunk(Call(10, 90, 0)), 1);

        store.Add(OpenFunction, Child, Chunk(Call(20, 40, 1)), 1);

        store.Add(BuildFunction, PlanObject, Chunk(Call(1, 8, 2)), 1);

        store.Add(ConstructorFunction, 0, Chunk(Call(100, 120, 3, Parent)), 1);

        var calls = store.Build();

        calls.MapNodes(mapped);

        var memory = MemoryIndex.Build(allocations, frees ?? []);

        return IteratorAllocations.Build(memory,
                                         calls,
                                         new HashSet<ulong> { Parent, Child },
                                         [new IteratorPlanNode(Child, PlanObject, 0, 0, 1)]);
    }

    private static ulong[] Call(ulong start, ulong end, int node, ulong instance = 0)
        => Row(start, flags: Thread | TraceCallColumns.ReturnedFlag, rcx: instance, node: node, endSequence: end);

    private static CallstackFrame Frame(string className, string method, uint rva) => new()
    {
        Module = "sqlmin",
        Rva = rva,
        Address = OpenFunction,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = SymbolCategory.Unknown
        }
    };
}
