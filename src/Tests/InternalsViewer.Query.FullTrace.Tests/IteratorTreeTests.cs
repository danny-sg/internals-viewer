using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Categories;
using InternalsViewer.Query.FullTrace.Batches;
using InternalsViewer.Query.FullTrace.Batches.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Iterators;
using static InternalsViewer.Query.FullTrace.Tests.TraceCallRows;
using static InternalsViewer.Query.FullTrace.Tests.TracePositions;

namespace InternalsViewer.Query.FullTrace.Tests;

[Trait("Category", "Unit")]
public class IteratorTreeTests
{
    private const ulong Thread = 7;

    private const ulong FilterConstructor = 0x1000;

    private const ulong ScanConstructor = 0x2000;

    private const ulong Filter = 0x1F4_0000_1000;

    private const ulong Aggregate = 0x1F4_0000_2000;

    private const ulong Scan = 0x1F4_0000_3000;

    [Fact]
    public void An_Iterator_Built_Inside_Another_Iterators_Constructor_Is_Its_Child()
    {
        var tree = Build([(FilterConstructor, Filter, 10, 100), (FilterConstructor, Aggregate, 20, 80), (ScanConstructor, Scan, 30, 40)]);

        Assert.Equal((0UL, Filter, Aggregate), (tree.GetParent(Filter), tree.GetParent(Aggregate), tree.GetParent(Scan)));
    }

    [Fact]
    public void A_Base_Constructor_On_The_Same_Iterator_Is_Not_Its_Parent()
    {
        var tree = Build([(FilterConstructor, Filter, 10, 100), (ScanConstructor, Scan, 20, 60), (FilterConstructor, Scan, 30, 40)]);

        Assert.Equal(Filter, tree.GetParent(Scan));
    }

    [Fact]
    public void A_Batch_Iterator_Is_The_Child_Of_The_Iterator_It_Hands_Batches_To()
    {
        var batches = new BatchStore([new BatchCapture(0x3000, Scan, 0, At(50), Scan, 0x1F4_0000_9000, Aggregate,
                                                       BatchCaptureFlags.None, [], [])]);

        var tree = Build([(FilterConstructor, Filter, 10, 100), (FilterConstructor, Aggregate, 20, 80)], batches);

        Assert.Equal(Aggregate, tree.GetParent(Scan));
    }

    private static IteratorTree Build((ulong Function, ulong Iterator, ulong From, ulong To)[] constructors, BatchStore? batches = null)
    {
        var tree = new CallStackTree();

        CallStackNode[] nodes = [tree.AddCall(tree.Root, Frame(FilterConstructor, "CQScanFilterNew"), 1),
                                 tree.AddCall(tree.Root, Frame(ScanConstructor, "CQScanRangeNew"), 1)];

        var store = new TraceCallStore.Builder();

        foreach (var group in constructors.GroupBy(c => c.Function))
        {
            var node = group.Key == FilterConstructor ? 0 : 1;

            ulong[][] rows = [.. group.Select(c => Row(c.From,
                                                       flags: Thread | TraceCallColumns.ReturnedFlag,
                                                       rcx: c.Iterator,
                                                       node: node,
                                                       endSequence: c.To))];

            store.Add(group.Key, 0, Chunk(rows), rows.Length);
        }

        var calls = store.Build();

        calls.MapNodes(nodes);

        return IteratorTree.Build(calls, nodes, batches ?? BatchStore.Empty);
    }

    private static CallstackFrame Frame(ulong address, string className) => new()
    {
        Module = "sqlmin",
        Rva = (uint)address,
        Address = address,
        Resolved = new ResolvedCallstackFrame
        {
            ClassName = className,
            MethodName = className,
            SymbolCategory = SymbolCategory.QueryOperator
        }
    };
}
