using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Parsing.Plans;

namespace InternalsViewer.Query.Tests.UnitTests.Events.Operators;

public class ScanModeDetectorTests
{
    private static readonly PageAddress IamPage = new(1, 157);

    [Fact]
    public void Heap_Is_Always_Allocation_Ordered()
    {
        var result = ScanModeDetector.Detect(ScanNode(), Unit(indexId: 0), []);

        Assert.Equal(ScanMode.AllocationOrdered, result!.Mode);
    }

    [Fact]
    public void Ordered_Scan_Uses_The_Leaf_Chain()
    {
        var node = ScanNode(isOrdered: true);

        var result = ScanModeDetector.Detect(node, Unit(indexId: 1), []);

        Assert.Equal(ScanMode.LeafChain, result!.Mode);
    }

    [Fact]
    public void Iam_Read_Confirms_Allocation_Order()
    {
        var result = ScanModeDetector.Detect(ScanNode(), Unit(indexId: 1), [Read(IamPage)]);

        Assert.Equal(ScanMode.AllocationOrdered, result!.Mode);
        Assert.Contains("IAM", result.Evidence);
    }

    [Fact]
    public void Pfs_Read_Confirms_Allocation_Order()
    {
        var result = ScanModeDetector.Detect(ScanNode(), Unit(indexId: 1), [Read(new PageAddress(1, 8088))]);

        Assert.Equal(ScanMode.AllocationOrdered, result!.Mode);
        Assert.Contains("PFS", result.Evidence);
    }

    [Fact]
    public void No_Allocation_Reads_Is_Indeterminate()
    {
        var result = ScanModeDetector.Detect(ScanNode(), Unit(indexId: 1), [Read(new PageAddress(1, 200))]);

        Assert.Equal(ScanMode.Indeterminate, result!.Mode);
    }

    [Fact]
    public void Reads_From_Other_Operators_Are_Ignored()
    {
        var read = Read(IamPage);

        read.PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 0, NodeId = 99 };

        var result = ScanModeDetector.Detect(ScanNode(), Unit(indexId: 1), [read]);

        Assert.Equal(ScanMode.Indeterminate, result!.Mode);
    }

    [Fact]
    public void Seek_Is_Not_Classified()
    {
        var node = ScanNode();

        node.PredicateInfo = new PredicateInfo { SeekBounds = [SeekBounds.All] };

        Assert.Null(ScanModeDetector.Detect(node, Unit(indexId: 1), []));
    }

    private static PlanNode ScanNode(bool? isOrdered = false)
    {
        return new PlanNode
        {
            NodeId = 5,
            Table = "[NumberTable]",
            ScanInfo = new ScanInfo { IsOutputOrdered = isOrdered }
        };
    }

    private static AllocationUnit Unit(int indexId)
    {
        return new AllocationUnit
        {
            IndexId = indexId,
            FirstIamPage = IamPage
        };
    }

    private static ReadEventGroup Read(PageAddress page)
    {
        return new ReadEventGroup
        {
            Events = [],
            Pages = [page],
            PlanNodeIdentifier = new PlanNodeIdentifier { PlanHandleId = 0, NodeId = 5 }
        };
    }
}
