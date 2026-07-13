using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Tests;

public class LockGroupingTests
{
    [Fact]
    public void Locks_On_Same_Object_And_Transaction_Are_Grouped()
    {
        var a = Lock(objectId: 42, transactionId: 100, sequenceId: 1, timeUs: 1_000);
        var b = Lock(objectId: 42, transactionId: 100, sequenceId: 2, timeUs: 2_000);

        var result = LockGrouping.Group([a, b]);

        var group = Assert.IsType<LockGroup>(Assert.Single(result));

        Assert.Equal(2, group.LockCount);
        Assert.Contains(a, group.Events);
        Assert.Contains(b, group.Events);
    }

    [Fact]
    public void Locks_On_Different_Objects_Are_Not_Grouped()
    {
        var a = Lock(objectId: 42, transactionId: 100, sequenceId: 1, timeUs: 1_000);
        var b = Lock(objectId: 99, transactionId: 100, sequenceId: 2, timeUs: 2_000);

        var result = LockGrouping.Group([a, b]);

        // One lock each on its object — neither reaches a group of two.
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e is LockGroup);
    }

    [Fact]
    public void Locks_In_Different_Transactions_Are_Not_Grouped()
    {
        var a = Lock(objectId: 42, transactionId: 100, sequenceId: 1, timeUs: 1_000);
        var b = Lock(objectId: 42, transactionId: 200, sequenceId: 2, timeUs: 2_000);

        var result = LockGrouping.Group([a, b]);

        Assert.DoesNotContain(result, e => e is LockGroup);
    }

    [Fact]
    public void Locks_Without_A_Resolved_Object_Stay_Individual()
    {
        var a = Lock(objectId: 42, transactionId: 100, sequenceId: 1, timeUs: 1_000);
        var b = Lock(objectId: 42, transactionId: 100, sequenceId: 2, timeUs: 2_000);

        // No allocation unit resolved (a metadata/database lock) — cannot be attributed to an object.
        var unresolved = new LockEvent
        {
            Name = "Lock",
            Resource = new LockResource(),
            LockOwnerContext = new LockOwnerContext { TransactionId = 100 },
            SequenceId = 3
        };

        var result = LockGrouping.Group([a, b, unresolved]);

        Assert.Contains(result, e => e is LockGroup);
        Assert.Contains(unresolved, result);
    }

    private static LockEvent Lock(int objectId, long transactionId, int sequenceId, long timeUs) => new()
    {
        Name = "Lock",
        Resource = new LockResource { ResourceType = LockResourceType.Key },
        LockOwnerContext = new LockOwnerContext { TransactionId = transactionId },
        AllocationUnit = new AllocationUnit { ObjectId = objectId },
        SequenceId = sequenceId,
        TimeUs = timeUs,
    };
}
