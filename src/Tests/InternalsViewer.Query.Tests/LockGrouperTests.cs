using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
public class LockGrouperTests
{
    [Fact]
    public void Locks_On_Same_Object_And_Transaction_Are_Grouped()
    {
        var a = Lock(objectId: 42, transactionId: 100, sequenceId: 1, timeUs: 1_000);
        var b = Lock(objectId: 42, transactionId: 100, sequenceId: 2, timeUs: 2_000);

        var result = LockGrouper.Group([a, b]);

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

        var result = LockGrouper.Group([a, b]);

        // One lock each on its object — neither reaches a group of two.
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, e => e is LockGroup);
    }

    [Fact]
    public void Locks_In_Different_Transactions_Are_Not_Grouped()
    {
        var a = Lock(objectId: 42, transactionId: 100, sequenceId: 1, timeUs: 1_000);
        var b = Lock(objectId: 42, transactionId: 200, sequenceId: 2, timeUs: 2_000);

        var result = LockGrouper.Group([a, b]);

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

        var result = LockGrouper.Group([a, b, unresolved]);

        Assert.Contains(result, e => e is LockGroup);
        Assert.Contains(unresolved, result);
    }

    [Fact]
    public void Schema_Locks_Group_Separately_From_The_Data_Locks()
    {
        // Same object + transaction, but schema-stability locks guard the object's shape, not its rows, so they form
        // their own "Object Schema Locks" group apart from the data-lock chain.
        var data1 = Lock(objectId: 42, transactionId: 100, sequenceId: 1, timeUs: 1_000);
        var data2 = Lock(objectId: 42, transactionId: 100, sequenceId: 2, timeUs: 2_000);

        var schema1 = Lock(objectId: 42, transactionId: 100, sequenceId: 3, timeUs: 3_000, mode: LockMode.SCH_S);
        var schema2 = Lock(objectId: 42, transactionId: 100, sequenceId: 4, timeUs: 4_000, mode: LockMode.SCH_S);

        var groups = LockGrouper.Group([data1, data2, schema1, schema2]).OfType<LockGroup>().ToList();

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.Name == "Object Locks" && g.Events.Contains(data1) && g.Events.Contains(data2));
        Assert.Contains(groups, g => g.Name == "Object Schema Locks"
                                     && g.Events.Contains(schema1) && g.Events.Contains(schema2));
    }

    [Fact]
    public void A_Lock_Group_Takes_Its_Anchor_Locks_Sequence_Id()
    {
        // Scope selection and grid highlighting range-test SequenceId, so a group left at the default 0 drags any
        // selected window's minimum down to the start of the trace.
        var first = Lock(objectId: 42, transactionId: 100, sequenceId: 300, timeUs: 1_000);

        var second = Lock(objectId: 42, transactionId: 100, sequenceId: 400, timeUs: 2_000);

        var group = Assert.IsType<LockGroup>(Assert.Single(LockGrouper.Group([first, second])));

        Assert.Equal(300, group.SequenceId);
    }

    private static LockEvent Lock(int objectId,
                                  long transactionId,
                                  int sequenceId,
                                  long timeUs,
                                  LockMode mode = LockMode.NL) => new()
    {
        Name = "Lock",
        LockMode = mode,
        Resource = new LockResource { ResourceType = LockResourceType.Key },
        LockOwnerContext = new LockOwnerContext { TransactionId = transactionId },
        AllocationUnit = new AllocationUnit { ObjectId = objectId },
        SequenceId = sequenceId,
        TimeUs = timeUs,
    };
}
