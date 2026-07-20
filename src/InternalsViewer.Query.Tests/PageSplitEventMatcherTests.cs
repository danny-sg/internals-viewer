using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.Splits;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Tests;

public class PageSplitEventMatcherTests
{
    private static readonly PageAddress SplitPage = new(1, 100);

    private static readonly PageAddress NewPage = new(1, 200);

    private static LogSequenceNumber Lsn(int sequence) => new(0x33, 0x1000, (short)sequence);

    [Fact]
    public void Attaches_Records_Targeting_The_Split_And_New_Pages_And_The_Format_Transaction()
    {
        var formatRecord = new FormatPageLogRecord
        {
            Lsn = Lsn(3),
            PreviousLsn = default,
            PageAddress = NewPage,
            TransactionId = 500
        };

        var allocationRecord = new SetBitsLogRecord
        {
            Lsn = Lsn(2),
            PreviousLsn = default,
            PageAddress = new PageAddress(1, 2),
            TransactionId = 500
        };

        var splitPageRecord = new DeleteRowsLogRecord
        {
            Lsn = Lsn(4),
            PreviousLsn = default,
            PageAddress = SplitPage,
            TransactionId = 400
        };

        var unrelatedRecord = new InsertRowsLogRecord
        {
            Lsn = Lsn(5),
            PreviousLsn = default,
            PageAddress = new PageAddress(1, 999),
            TransactionId = 400
        };

        var splitEvent = new PageSplitEvent
        {
            Name = "page_split",
            PageAddress = SplitPage,
            NewPage = NewPage,
            SplitOperation = PageSplitOperation.SPLIT_FOR_INSERT
        };

        PageSplitEventMatcher.Match([splitEvent],
                                    [unrelatedRecord, splitPageRecord, formatRecord, allocationRecord]);

        Assert.Equal([allocationRecord, formatRecord, splitPageRecord],
                     splitEvent.LogRecords.Cast<object>());
    }

    [Fact]
    public void Leaves_Split_Event_Empty_When_No_Records_Relate()
    {
        var splitEvent = new PageSplitEvent
        {
            Name = "page_split",
            PageAddress = SplitPage,
            NewPage = NewPage
        };

        var unrelatedRecord = new InsertRowsLogRecord
        {
            Lsn = Lsn(1),
            PreviousLsn = default,
            PageAddress = new PageAddress(1, 999),
            TransactionId = 400
        };

        PageSplitEventMatcher.Match([splitEvent], [unrelatedRecord]);

        Assert.Empty(splitEvent.LogRecords);
    }
}
