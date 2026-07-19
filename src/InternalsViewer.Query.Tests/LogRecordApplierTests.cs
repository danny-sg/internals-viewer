using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Query.TransactionLog;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.Tests;

public class LogRecordApplierTests
{
    private static readonly PageAddress Address = new(1, 100);

    private static readonly LogSequenceNumber InitialLsn = new(0x33, 0x1000, 1);

    private static readonly LogSequenceNumber RecordLsn = new(0x33, 0x2000, 1);

    private static PageData CreatePage(ushort rowOffset = 96)
    {
        return new PageData
        {
            PageAddress = Address,
            Data = new byte[PageData.Size],
            PageHeader = new PageHeader { Lsn = InitialLsn },
            OffsetTable = [rowOffset]
        };
    }

    [Fact]
    public void Applies_Same_Size_Modify_Row_Splice_And_Stamps_Page_Lsn()
    {
        var page = CreatePage();

        page.Data[100] = 0xAA;
        page.Data[101] = 0xBB;

        var record = new ModifyRowLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            SlotId = 0,
            OffsetInRow = 4,
            ModifySize = 2,
            BeforeData = [0xAA, 0xBB],
            AfterData = [0xCC, 0xDD]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([0xCC, 0xDD], page.Data[100..102]);
        Assert.Equal(RecordLsn, page.PageHeader.Lsn);
        Assert.Equal(0x33, BitConverter.ToInt32(page.Data, 40));
        Assert.Equal(0x2000, BitConverter.ToInt32(page.Data, 44));
    }

    [Fact]
    public void Rejects_Record_When_Page_Lsn_Does_Not_Match_Chain()
    {
        var page = CreatePage();

        var record = new ModifyRowLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = new LogSequenceNumber(0x33, 0x1500, 1),
            SlotId = 0,
            ModifySize = 0
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.LsnMismatch, result.Status);
        Assert.Equal(InitialLsn, page.PageHeader.Lsn);
    }

    [Fact]
    public void Rejects_Splice_When_Page_Bytes_Do_Not_Match_Before_Image()
    {
        var page = CreatePage();

        var record = new ModifyRowLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            SlotId = 0,
            OffsetInRow = 4,
            ModifySize = 2,
            BeforeData = [0xAA, 0xBB],
            AfterData = [0xCC, 0xDD]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.BeforeImageMismatch, result.Status);
        Assert.Equal(InitialLsn, page.PageHeader.Lsn);
    }

    [Fact]
    public void Reports_Size_Changing_Splice_As_Not_Supported()
    {
        var page = CreatePage();

        var record = new ModifyRowLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            SlotId = 0,
            OffsetInRow = 4,
            ModifySize = 5,
            BeforeData = [1, 2, 3, 4, 5],
            AfterData = [9]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.NotSupported, result.Status);
    }

    [Fact]
    public void Applies_Set_Free_Space_To_Pfs_Byte()
    {
        var page = CreatePage();

        page.Data[100 + 2228] = 0x40;

        var record = new SetFreeSpaceLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            PageOffset = 2228,
            OldValue = 0x40,
            NewValue = 0x41
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal(0x41, page.Data[100 + 2228]);
    }

    [Fact]
    public void Replays_Chained_Records_In_Lsn_Order_Up_To_Target()
    {
        var page = CreatePage();

        page.Data[100] = 0x01;

        var first = new ModifyRowLogRecord
        {
            Lsn = new LogSequenceNumber(0x33, 0x2000, 1),
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            SlotId = 0,
            OffsetInRow = 4,
            ModifySize = 1,
            BeforeData = [0x01],
            AfterData = [0x02]
        };

        var second = new ModifyRowLogRecord
        {
            Lsn = new LogSequenceNumber(0x33, 0x3000, 1),
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = first.Lsn,
            SlotId = 0,
            OffsetInRow = 4,
            ModifySize = 1,
            BeforeData = [0x02],
            AfterData = [0x03]
        };

        var third = new ModifyRowLogRecord
        {
            Lsn = new LogSequenceNumber(0x33, 0x4000, 1),
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = second.Lsn,
            SlotId = 0,
            OffsetInRow = 4,
            ModifySize = 1,
            BeforeData = [0x03],
            AfterData = [0x04]
        };

        var result = LogRecordApplier.Replay(page, [third, first, second], second.Lsn);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal(0x03, page.Data[100]);
        Assert.Equal(second.Lsn, page.PageHeader.Lsn);
    }
}
