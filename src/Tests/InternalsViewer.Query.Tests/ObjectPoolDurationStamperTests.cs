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

        var decision = new ColumnStoreScanEvent { EventName = "large_cache_caching_decision", SequenceId = 35, TimeUs = 2400, TaskAddress = 1 };

        var miss = new ObjectPoolEvent { IsHit = false, SequenceId = 40, TimeUs = 2500, TaskAddress = 1, PlanNodeIdentifier = Scan };

        var secondMiss = new ObjectPoolEvent { IsHit = false, SequenceId = 50, TimeUs = 2600, TaskAddress = 1, PlanNodeIdentifier = Scan };

        ObjectPoolDurationStamper.Stamp([miss, secondRead, readIssued, decision, firstRead, secondMiss]);

        Assert.Equal(1200, miss.TimeUs);
        Assert.Equal(1300, miss.DurationUs);
        Assert.Equal(firstRead.Timestamp, miss.Timestamp);
        Assert.Equal(2600, secondMiss.TimeUs);
        Assert.Equal(0, secondMiss.DurationUs);
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
}
