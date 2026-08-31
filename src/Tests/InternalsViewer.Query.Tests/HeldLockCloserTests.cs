using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Transactions;

namespace InternalsViewer.Query.Tests;

[Trait("Category", "Unit")]
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

    [Fact]
    public void A_Fine_Lock_Taken_In_The_Same_Instant_As_The_Escalation_Is_Still_Dropped_By_It()
    {
        // Timestamps resolve to the millisecond, so the last locks taken before an escalation routinely share its
        // bucket. Requiring the escalation to be strictly after would send exactly those to the statement end, leaving
        // the last page's locks held for the whole trace.
        var key = Lock(LockResourceType.Key, LockMode.RS_U, txn: 1, timeUs: 5_000);

        var escalated = Escalation(txn: 1, timeUs: 5_000);

        HeldLockCloser.Close([key, escalated, Other(timeUs: 20_000)]);

        Assert.Equal(0, key.DurationUs); // dropped at the escalation, not held to 20_000
        Assert.Equal("Lock", key.Name);
    }

    [Fact]
    public void A_Fine_Lock_Taken_After_The_Escalation_Falls_Back_To_The_Statement_End()
    {
        // Genuinely later than the escalation (a different bucket), so it cannot have been dropped by it.
        var key = Lock(LockResourceType.Key, LockMode.RS_U, txn: 1, timeUs: 7_000);

        var escalated = Escalation(txn: 1, timeUs: 5_000);

        HeldLockCloser.Close([key, escalated, Other(timeUs: 20_000)]);

        Assert.Equal(13_000, key.DurationUs);
    }

    [Fact]
    public void A_Measured_Escalation_Event_Beats_The_Inferred_Escalation_Point()
    {
        var key = Lock(LockResourceType.Key, LockMode.RS_U, txn: 1, timeUs: 1_000);

        // The lock_escalation event says escalation fired at 3_000; the object X lock's acquire (the fallback
        // inference) only shows at 5_000. The measured moment wins.
        var escalated = Escalation(txn: 1, timeUs: 3_000);

        var objectLock = Paired(LockResourceType.Object, LockMode.X, txn: 1, timeUs: 5_000, durationUs: 2_000);

        HeldLockCloser.Close([key, escalated, objectLock, Other(timeUs: 20_000)]);

        Assert.Equal(2_000, key.DurationUs); // 1_000 -> 3_000
    }

    [Fact]
    public void A_Held_Lock_Is_Closed_At_Its_Transactions_Commit_Rather_Than_The_Statement_End()
    {
        var key = Lock(LockResourceType.Key, LockMode.RS_S, txn: 1, timeUs: 1_000);

        var commit = Transaction(txn: 1, TransactionState.Commit, timeUs: 8_000);

        HeldLockCloser.Close([key, commit, Other(timeUs: 20_000)]);

        Assert.Equal(7_000, key.DurationUs); // 1_000 -> 8_000 (the commit), not to 20_000
    }

    [Fact]
    public void A_Commit_Before_The_Lock_Is_Ignored()
    {
        // Guards a mis-mapped transaction_state: an "end" that precedes the lock cannot be its release, so the
        // statement-end fallback stands rather than producing a zero-length hold.
        var key = Lock(LockResourceType.Key, LockMode.RS_S, txn: 1, timeUs: 5_000);

        var bogus = Transaction(txn: 1, TransactionState.Commit, timeUs: 2_000);

        HeldLockCloser.Close([key, bogus, Other(timeUs: 20_000)]);

        Assert.Equal(15_000, key.DurationUs);
    }

    [Fact]
    public void Another_Transactions_Commit_Does_Not_Close_The_Lock()
    {
        var key = Lock(LockResourceType.Key, LockMode.RS_S, txn: 1, timeUs: 1_000);

        var otherCommit = Transaction(txn: 2, TransactionState.Commit, timeUs: 8_000);

        HeldLockCloser.Close([key, otherCommit, Other(timeUs: 20_000)]);

        Assert.Equal(19_000, key.DurationUs); // statement end
    }

    [Fact]
    public void The_Statement_End_Extends_To_The_Last_Events_Interval_End()
    {
        // The collapsed stream carries durations, so the statement runs until the tail event's interval ENDS. A lock
        // acquired at the tail's start would otherwise close as the invisible zero-length hold this class exists to fix.
        var key = Lock(LockResourceType.Key, LockMode.RS_U, txn: 1, timeUs: 20_000);

        var tail = Other(timeUs: 20_000);

        tail.DurationUs = 5_000;

        HeldLockCloser.Close([key, tail]);

        Assert.Equal(5_000, key.DurationUs);
        Assert.Equal("Lock", key.Name);
    }

    private static LockEscalationEvent Escalation(long txn, long timeUs) => new()
    {
        Name = "lock_escalation",
        LockMode = LockMode.X,
        ResourceType = LockResourceType.Object,
        TransactionId = txn,
        TimeUs = timeUs,
    };

    private static TransactionEvent Transaction(long txn, TransactionState state, long timeUs) => new()
    {
        Name = "sql_transaction",
        TransactionId = txn,
        State = state,
        TimeUs = timeUs,
    };

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
