using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Consolidation;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Tests;

public class ObjectPoolDurationStamperTests
{
    private static readonly PlanNodeIdentifier Scan = new() { NodeId = 1, PlanHandleId = 1 };

    [Fact]
    public void Stamp_Starts_A_Miss_At_The_First_Read_Since_The_Previous_Columnstore_Event()
    {
        var readIssued = new ColumnStoreScanEvent { EventName = "column_store_rowgroup_read_issued", SequenceId = 10, TimeUs = 1000, TaskAddress = 1 };

        var firstRead = new ReadEventGroup { Events = [], SequenceId = 20, TimeUs = 1200, TaskAddress = 1, PlanNodeIdentifier = Scan, Timestamp = new DateTime(2026, 1, 1, 0, 0, 1) };

        var secondRead = new ReadEventGroup { Events = [], SequenceId = 30, TimeUs = 1800, TaskAddress = 1, PlanNodeIdentifier = Scan };

        var miss = new ObjectPoolEvent { IsHit = false, SequenceId = 40, TimeUs = 2500, TaskAddress = 1, PlanNodeIdentifier = Scan };

        var secondMiss = new ObjectPoolEvent { IsHit = false, SequenceId = 50, TimeUs = 2600, TaskAddress = 1, PlanNodeIdentifier = Scan };

        ObjectPoolDurationStamper.Stamp([miss, secondRead, readIssued, firstRead, secondMiss]);

        Assert.Equal(1200, miss.TimeUs);
        Assert.Equal(1300, miss.DurationUs);
        Assert.Equal(firstRead.Timestamp, miss.Timestamp);
        Assert.Equal(2600, secondMiss.TimeUs);
        Assert.Equal(0, secondMiss.DurationUs);
    }

    [Fact]
    public void Stamp_Uses_The_Reads_Of_A_Misss_Own_Pages_When_They_Are_Known()
    {
        var ownPage = new PageAddress(1, 100);

        var otherPage = new PageAddress(1, 200);

        var readAhead = new ReadEventGroup { Events = [], Pages = [ownPage], SequenceId = 1, TimeUs = 400, TaskAddress = 1, PlanNodeIdentifier = Scan };

        var boundary = new ColumnStoreScanEvent { EventName = "column_store_rowgroup_read_issued", SequenceId = 2, TimeUs = 1000, TaskAddress = 1 };

        var unrelated = new ReadEventGroup { Events = [], Pages = [otherPage], SequenceId = 3, TimeUs = 1200, TaskAddress = 1, PlanNodeIdentifier = Scan };

        var miss = new ObjectPoolEvent { IsHit = false, Pages = [ownPage], SequenceId = 4, TimeUs = 2000, TaskAddress = 1, PlanNodeIdentifier = Scan };

        var otherMiss = new ObjectPoolEvent { IsHit = false, Pages = [new PageAddress(1, 300)], SequenceId = 5, TimeUs = 2100, TaskAddress = 1, PlanNodeIdentifier = Scan };

        EngineEvent[] events = [readAhead, boundary, unrelated, miss, otherMiss];

        ObjectPoolReadLinker.Link(events);

        ObjectPoolDurationStamper.Stamp(events);

        Assert.Equal(400, miss.TimeUs);
        Assert.Equal(1600, miss.DurationUs);
        Assert.Equal(2100, otherMiss.TimeUs);
        Assert.Equal(0, otherMiss.DurationUs);
    }

    [Fact]
    public void Stamp_Stretches_A_Miss_Over_Reads_That_Preceded_It_In_Sequence_But_Were_Spread_Later()
    {
        var page = new PageAddress(1, 100);

        var before = new ReadEventGroup { Events = [], Pages = [page], SequenceId = 1, TimeUs = 5_000, DurationUs = 50, TaskAddress = 1 };

        var spreadLater = new ReadEventGroup { Events = [], Pages = [page], SequenceId = 2, TimeUs = 5_800, DurationUs = 100, TaskAddress = 1 };

        var miss = new ObjectPoolEvent { IsHit = false, Pages = [page], SequenceId = 3, TimeUs = 5_500, TaskAddress = 1 };

        var afterwards = new ReadEventGroup { Events = [], Pages = [page], SequenceId = 4, TimeUs = 9_000, DurationUs = 100, TaskAddress = 1 };

        EngineEvent[] events = [before, spreadLater, miss, afterwards];

        ObjectPoolReadLinker.Link(events);

        ObjectPoolDurationStamper.Stamp(events);

        Assert.Same(miss, afterwards.PoolLookup);
        Assert.Equal(5_000, miss.TimeUs);
        Assert.Equal(900, miss.DurationUs);
    }

    [Fact]
    public void Stamp_Ignores_Reads_From_Other_Nodes_Tasks_And_Hits()
    {
        var catalogRead = new ReadEventGroup { Events = [], SequenceId = 1, TimeUs = 100, TaskAddress = 1 };

        var otherTaskRead = new ReadEventGroup { Events = [], SequenceId = 2, TimeUs = 200, TaskAddress = 2, PlanNodeIdentifier = Scan };

        var miss = new ObjectPoolEvent { IsHit = false, SequenceId = 3, TimeUs = 900, TaskAddress = 1, PlanNodeIdentifier = Scan };

        var read = new ReadEventGroup { Events = [], SequenceId = 4, TimeUs = 950, TaskAddress = 1, PlanNodeIdentifier = Scan };

        var hit = new ObjectPoolEvent { IsHit = true, SequenceId = 5, TimeUs = 1000, TaskAddress = 1, PlanNodeIdentifier = Scan };

        ObjectPoolDurationStamper.Stamp([catalogRead, otherTaskRead, miss, read, hit]);

        Assert.Equal(900, miss.TimeUs);
        Assert.Equal(0, miss.DurationUs);
        Assert.Equal(1000, hit.TimeUs);
        Assert.Equal(0, hit.DurationUs);
    }

    [Fact]
    public void Stamp_Does_Not_Stretch_A_Miss_Back_To_Its_Read_Ahead()
    {
        var page = new PageAddress(1, 100);

        var readAhead = new ReadEventGroup
        {
            Events = [],
            Pages = [page],
            ReadType = ReadType.NonCached,
            SequenceId = 1,
            TimeUs = 100,
            DurationUs = 50,
            TaskAddress = 1,
        };

        var earlierMiss = new ObjectPoolEvent { IsHit = false, Pages = [new PageAddress(1, 300)], SequenceId = 2, TimeUs = 500, TaskAddress = 1 };

        var build = new ReadEventGroup { Events = [], Pages = [page], ReadType = ReadType.Cached, SequenceId = 3, TimeUs = 9_000, TaskAddress = 1 };

        var miss = new ObjectPoolEvent { IsHit = false, Pages = [page], SequenceId = 4, TimeUs = 9_400, TaskAddress = 1 };

        EngineEvent[] events = [readAhead, earlierMiss, build, miss];

        ObjectPoolReadLinker.Link(events);

        ObjectPoolDurationStamper.Stamp(events);

        Assert.Equal(9_000, miss.TimeUs);
        Assert.Equal(400, miss.DurationUs);
    }
}
