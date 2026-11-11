using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Tree;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRowHelper;
using static InternalsViewer.Query.FullTrace.Tests.TracePositionHelper;

namespace InternalsViewer.Query.FullTrace.Tests.Memory.Allocation;

[Trait("Category", "Unit")]
public class AllocationOwnerResolverTests
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
    public void An_Iterator_Object_Belongs_To_That_Iterator()
    {
        Assert.Equal([Child], GetOwners(Block(Child, 5)));
    }

    [Fact]
    public void A_Block_Allocated_Inside_An_Iterator_Call_Belongs_To_The_Innermost_Iterator()
    {
        Assert.Equal([Child, Parent], GetOwners(Block(Buffer, 30), Block(Buffer + 0x1000, 60)));
    }

    [Fact]
    public void A_Block_Allocated_While_A_Plan_Object_Builds_Its_Iterator_Belongs_To_That_Iterator()
    {
        Assert.Equal([Child], GetOwners(Block(Buffer, 3)));
    }

    [Fact]
    public void A_Block_Allocated_Inside_An_Iterator_Constructor_Belongs_To_That_Iterator()
    {
        Assert.Equal([Parent], GetOwners(Block(Buffer, 110)));
    }

    [Fact]
    public void An_Earlier_Object_At_An_Iterators_Address_Is_Not_That_Iterator()
    {
        Assert.Equal([0UL, Child], GetOwners(Block(Child, 9), Block(Child, 15)));
    }

    [Fact]
    public void An_Object_Allocated_At_An_Iterators_Address_After_It_Ran_Is_Not_That_Iterator()
    {
        Assert.Equal([0UL], GetOwners(Block(Child, 95)));
    }

    [Fact]
    public void A_Block_Allocated_Outside_Every_Iterator_Call_Has_No_Owner()
    {
        Assert.Equal([0UL], GetOwners(Block(Buffer, 95)));
    }

    private static MemoryBlock Block(ulong pointer, ulong start) => new(pointer, 0x100, Thread, At(start), TracePosition.Unknown);

    private static ulong[] GetOwners(params MemoryBlock[] blocks)
    {
        var tree = new CallTree();

        CallTreeNode[] mapped =
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

        return AllocationOwnerResolver.GetOwners(blocks,
                                                 calls,
                                                 new HashSet<ulong> { Parent, Child },
                                                 new Dictionary<ulong, TracePosition> { [Parent] = At(10), [Child] = At(20) },
                                                 [new IteratorPlanNode(Child, PlanObject, 0, 0, 1)]);
    }

    private static ulong[] Call(ulong start, ulong end, int callNodeIndex, ulong instance = 0)
        => Row(start, flags: Thread | TraceCallColumnSet.ReturnedFlag, rcx: instance, callNodeIndex: callNodeIndex, endSequence: end);

    private static CallFrame Frame(string className, string method, uint rva) => new()
    {
        Module = new("sqlmin"),
        Rva = rva,
        Address = OpenFunction,
        Function = new FrameFunction
        {
            ClassName = className,
            MethodName = method,
            SymbolCategory = SymbolCategory.Unknown
        }
    };
}
