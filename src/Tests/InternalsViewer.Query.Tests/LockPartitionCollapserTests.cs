using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class LockPartitionCollapserTests
{
    [Fact]
    public void An_Object_Lock_Sweep_Folds_To_One_Lock_Spanning_The_Partitions()
    {
        // A TABLOCKX on a 16+ CPU server takes X on every lock partition; they are one table lock.
        var p0 = ObjectLock(partition: 0, timeUs: 1_000, durationUs: 5_000);
        var p1 = ObjectLock(partition: 1, timeUs: 1_100, durationUs: 5_000);
        var p2 = ObjectLock(partition: 2, timeUs: 1_200, durationUs: 5_000);

        var result = LockPartitionCollapser.Collapse([p0, p1, p2]);

        var folded = Assert.IsType<LockEvent>(Assert.Single(result));

        // The earliest acquire keeps its place and takes the envelope: first acquire to last release.
        Assert.Same(p0, folded);
        Assert.Equal(1_000, folded.TimeUs);
        Assert.Equal(5_200, folded.DurationUs);
        Assert.Equal(3, folded.PartitionCount);
    }

    [Fact]
    public void An_Intent_Lock_Is_Left_Alone()
    {
        // Intent modes are only ever taken on one partition, so there is no sweep to fold.
        var ix = ObjectLock(partition: 7, timeUs: 1_000, durationUs: 5_000, mode: LockMode.IX);

        var result = LockPartitionCollapser.Collapse([ix]);

        Assert.Same(ix, Assert.Single(result));
        Assert.Equal(1, ix.PartitionCount);
        Assert.Equal(5_000, ix.DurationUs);
    }

    [Fact]
    public void Repeated_Intent_Locks_Are_Not_Folded_Together()
    {
        // Two IX acquisitions land on whichever scheduler ran them - distinct partitions, but not a sweep.
        var first = ObjectLock(partition: 3, timeUs: 1_000, durationUs: 100, mode: LockMode.IX);
        var second = ObjectLock(partition: 9, timeUs: 8_000, durationUs: 100, mode: LockMode.IX);

        var result = LockPartitionCollapser.Collapse([first, second]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Sweeps_In_Different_Transactions_Fold_Separately()
    {
        var a0 = ObjectLock(partition: 0, timeUs: 1_000, durationUs: 100, transactionId: 100);
        var a1 = ObjectLock(partition: 1, timeUs: 1_050, durationUs: 100, transactionId: 100);

        var b0 = ObjectLock(partition: 0, timeUs: 9_000, durationUs: 100, transactionId: 200);
        var b1 = ObjectLock(partition: 1, timeUs: 9_050, durationUs: 100, transactionId: 200);

        var result = LockPartitionCollapser.Collapse([a0, a1, b0, b1]);

        Assert.Equal(2, result.Count);
        Assert.Contains(a0, result);
        Assert.Contains(b0, result);
    }

    [Fact]
    public void Non_Object_Locks_Are_Left_Alone()
    {
        // Only OBJECT resources are partitioned; a KEY lock's resource_1 is part of its hash, not a partition.
        var a = new LockEvent
        {
            Name = "Lock",
            LockMode = LockMode.X,
            Resource = new LockResource { ResourceType = LockResourceType.Key, ObjectId = 42 },
            LockOwnerContext = new LockOwnerContext { TransactionId = 100 },
            TimeUs = 1_000,
        };

        var b = new LockEvent
        {
            Name = "Lock",
            LockMode = LockMode.X,
            Resource = new LockResource { ResourceType = LockResourceType.Key, ObjectId = 42 },
            LockOwnerContext = new LockOwnerContext { TransactionId = 100 },
            TimeUs = 2_000,
        };

        var result = LockPartitionCollapser.Collapse([a, b]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Non_Lock_Events_Pass_Through()
    {
        var p0 = ObjectLock(partition: 0, timeUs: 1_000, durationUs: 100);
        var p1 = ObjectLock(partition: 1, timeUs: 1_050, durationUs: 100);

        var other = new EngineEvent { Name = "wait_info", TimeUs = 500 };

        var result = LockPartitionCollapser.Collapse([other, p0, p1]);

        Assert.Equal(2, result.Count);
        Assert.Contains(other, result);
        Assert.Contains(p0, result);
    }

    private static LockEvent ObjectLock(int partition,
                                        long timeUs,
                                        long durationUs,
                                        LockMode mode = LockMode.X,
                                        long transactionId = 100) => new()
    {
        Name = "Lock",
        LockMode = mode,
        Resource = new LockResource
        {
            ResourceType = LockResourceType.Object,
            ObjectId = 42,
            LockPartition = partition,
        },
        LockOwnerContext = new LockOwnerContext { TransactionId = transactionId },
        TimeUs = timeUs,
        DurationUs = durationUs,
    };
}
