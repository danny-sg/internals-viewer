using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Locks;

namespace InternalsViewer.Query.Tests;

public class HeldLockCloserTests
{
    [Fact]
    public void A_Fine_Lock_Is_Held_Until_The_Transaction_Escalates_To_An_Object_Lock()
    {
        // A key lock the statement never releases individually is dropped when the transaction escalates to a full
        // object X lock, so its hold ends at that escalation point — not at the end of the statement.
        var key = Lock(LockResourceType.Key, LockMode.RS_U, txn: 1, timeUs: 1_000);

        var escalation = Paired(LockResourceType.Object, LockMode.X, txn: 1, timeUs: 5_000, durationUs: 2_000);

        var tail = Other(timeUs: 20_000); // sets the statement end well past the escalation

        HeldLockCloser.Close([key, escalation, tail]);

        Assert.Equal(4_000, key.DurationUs); // 1_000 -> 5_000 (the escalation acquire), not to 20_000
        Assert.Equal("Lock", key.Name);
    }

    [Fact]
    public void A_Fine_Lock_With_No_Escalation_Is_Held_To_The_Statement_End()
    {
        var key = Lock(LockResourceType.Key, LockMode.RS_U, txn: 1, timeUs: 1_000);

        var tail = Other(timeUs: 20_000);

        HeldLockCloser.Close([key, tail]);

        Assert.Equal(19_000, key.DurationUs);
    }

    [Fact]
    public void An_Intent_Object_Lock_Does_Not_Count_As_Escalation()
    {
        // IX is an intent lock held alongside the fine locks, not a superseding one, so it must not cut their hold.
        var key = Lock(LockResourceType.Key, LockMode.RS_U, txn: 1, timeUs: 1_000);

        var intent = Lock(LockResourceType.Object, LockMode.IX, txn: 1, timeUs: 2_000);

        var tail = Other(timeUs: 20_000);

        HeldLockCloser.Close([key, intent, tail]);

        Assert.Equal(19_000, key.DurationUs); // held to statement end, not to the IX intent lock
    }

    [Fact]
    public void A_Paired_Lock_Is_Left_Untouched()
    {
        var paired = Paired(LockResourceType.Key, LockMode.S, txn: 1, timeUs: 1_000, durationUs: 50);

        var tail = Other(timeUs: 20_000);

        HeldLockCloser.Close([paired, tail]);

        Assert.Equal(50, paired.DurationUs);
        Assert.Equal("Lock", paired.Name);
    }

    [Fact]
    public void A_Metadata_Lock_Is_Not_Held_To_End()
    {
        // Schema/metadata locks are transient; a missing release there is not a full-statement hold.
        var metadata = Lock(LockResourceType.Metadata, LockMode.SCH_S, txn: 1, timeUs: 1_000);

        var tail = Other(timeUs: 20_000);

        HeldLockCloser.Close([metadata, tail]);

        Assert.Equal(0, metadata.DurationUs);
        Assert.Equal("lock_acquired", metadata.Name);
    }

    [Fact]
    public void Escalation_Only_Closes_Locks_In_The_Same_Transaction()
    {
        var key = Lock(LockResourceType.Key, LockMode.RS_U, txn: 1, timeUs: 1_000);

        // A different transaction's object lock must not act as this key's escalation point.
        var otherTxnEscalation = Paired(LockResourceType.Object, LockMode.X, txn: 2, timeUs: 5_000, durationUs: 100);

        var tail = Other(timeUs: 20_000);

        HeldLockCloser.Close([key, otherTxnEscalation, tail]);

        Assert.Equal(19_000, key.DurationUs); // held to statement end (no escalation in txn 1)
    }

    private static LockEvent Lock(LockResourceType type, LockMode mode, long txn, long timeUs) => new()
    {
        Name = "lock_acquired",
        LockMode = mode,
        Resource = new LockResource { ResourceType = type, Key = (ulong)timeUs },
        LockOwnerContext = new LockOwnerContext { TransactionId = txn },
        TimeUs = timeUs,
        DurationUs = 0,
    };

    private static LockEvent Paired(LockResourceType type, LockMode mode, long txn, long timeUs, long durationUs)
    {
        var lockEvent = Lock(type, mode, txn, timeUs);

        lockEvent.Name = "Lock";
        lockEvent.DurationUs = durationUs;

        return lockEvent;
    }

    private static EngineEvent Other(long timeUs) => new() { Name = "batch", TimeUs = timeUs };
}
