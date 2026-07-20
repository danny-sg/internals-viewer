using System.Buffers.Binary;
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

        Assert.Equal(2, result.Changes.Count);
        Assert.Equal(new ChangeSpan(100, 2, "Slot 0 row modified at row offset 4"), result.Changes[0]);
        Assert.Equal(40, result.Changes[1].Offset);
        Assert.Equal(10, result.Changes[1].Length);
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
    public void Rejects_Size_Changing_Modify_Row_When_Range_Is_Beyond_The_Row()
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
    public void Rebase_Seeds_Pfs_Bytes_From_First_Record_Old_Values()
    {
        var page = CreateSlottedPage([96], freeData: 200, freeCount: 1000);

        // The baseline byte has drifted from the capture-time value (PFS changes are not rolled back)
        page.Data[100 + 2228] = 0x99;

        var first = new SetFreeSpaceLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            PageOffset = 2228,
            OldValue = 0x40,
            NewValue = 0x41
        };

        var second = new SetFreeSpaceLogRecord
        {
            Lsn = new LogSequenceNumber(0x33, 0x3000, 1),
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = first.Lsn,
            PageOffset = 2228,
            OldValue = 0x41,
            NewValue = 0x42
        };

        LogRecordApplier.Rebase(page, [first, second]);

        Assert.Equal(0x40, page.Data[100 + 2228]);
        Assert.Equal(InitialLsn, page.PageHeader.Lsn);

        var result = LogRecordApplier.Replay(page, [first, second], second.Lsn);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal(0x42, page.Data[100 + 2228]);
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

    private static PageData CreateSlottedPage(ushort[] slotOffsets, ushort freeData, ushort freeCount)
    {
        var page = new PageData
        {
            PageAddress = Address,
            Data = new byte[PageData.Size],
            PageHeader = new PageHeader
            {
                Lsn = InitialLsn,
                SlotCount = (ushort)slotOffsets.Length,
                FreeData = freeData,
                FreeCount = freeCount
            },
            OffsetTable = slotOffsets
        };

        for (var slotId = 0; slotId < slotOffsets.Length; slotId++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(page.Data.AsSpan(PageData.Size - 2 * (slotId + 1)),
                                                     slotOffsets[slotId]);
        }

        return page;
    }

    [Fact]
    public void Applies_Heap_Insert_As_Slot_Append()
    {
        var page = CreateSlottedPage([96], freeData: 120, freeCount: 1000);

        var record = new InsertRowsLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 1,
            RowData = [1, 2, 3, 4]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([1, 2, 3, 4], page.Data[120..124]);
        Assert.Equal([96, 120], page.OffsetTable.Select(o => (int)o));
        Assert.Equal(2, page.PageHeader.SlotCount);
        Assert.Equal(124, page.PageHeader.FreeData);
        Assert.Equal(994, page.PageHeader.FreeCount);

        Assert.Contains(result.Changes, c => c is { Offset: 120, Length: 4 });
        Assert.Contains(result.Changes,
                        c => c.Offset == PageData.Size - 4
                             && c.Length == 2
                             && c.Description == "Record added to slot offset table (slot 1)");
        Assert.Contains(result.Changes, c => c.Offset == 22);
        Assert.Contains(result.Changes, c => c.Offset == 28);
        Assert.Contains(result.Changes, c => c.Offset == 30);
        Assert.Contains(result.Changes, c => c.Offset == 40);
    }

    [Fact]
    public void Applies_Heap_Insert_Into_Zeroed_Slot_Without_Changing_Slot_Count()
    {
        var page = CreateSlottedPage([96, 0], freeData: 120, freeCount: 1000);

        var record = new InsertRowsLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 1,
            RowData = [1, 2, 3, 4]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([96, 120], page.OffsetTable.Select(o => (int)o));
        Assert.Equal(2, page.PageHeader.SlotCount);
        Assert.Equal(996, page.PageHeader.FreeCount);
    }

    [Fact]
    public void Applies_Heap_Delete_By_Zeroing_The_Slot_Entry()
    {
        var page = CreateSlottedPage([96, 120], freeData: 124, freeCount: 1000);

        page.Data[120] = 1;
        page.Data[121] = 2;
        page.Data[122] = 3;
        page.Data[123] = 4;

        var record = new DeleteRowsLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 1,
            RowData = [1, 2, 3, 4]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([96, 0], page.OffsetTable.Select(o => (int)o));
        Assert.Equal(2, page.PageHeader.SlotCount);
        Assert.Equal(1004, page.PageHeader.FreeCount);
        Assert.Equal(124, page.PageHeader.FreeData);
    }

    [Fact]
    public void Applies_Index_Insert_By_Shifting_Offset_Entries()
    {
        var page = CreateSlottedPage([96, 110], freeData: 130, freeCount: 1000);

        var record = new InsertRowsLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.CLUSTERED,
            SlotId = 1,
            RowData = [9, 9, 9]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([96, 130, 110], page.OffsetTable.Select(o => (int)o));
        Assert.Equal(3, page.PageHeader.SlotCount);
        Assert.Equal([9, 9, 9], page.Data[130..133]);
    }

    private static byte[] BuildRow(byte[] fixedData, byte[] variableData)
    {
        var columnCountPosition = 4 + fixedData.Length;

        var totalLength = columnCountPosition + 2 + 1 + 2 + 2 + variableData.Length;

        var row = new List<byte> { 0x30, 0x00 };

        row.AddRange(BitConverter.GetBytes((ushort)columnCountPosition));
        row.AddRange(fixedData);
        row.AddRange(BitConverter.GetBytes((ushort)2));
        row.Add(0x00);
        row.AddRange(BitConverter.GetBytes((ushort)1));
        row.AddRange(BitConverter.GetBytes((ushort)totalLength));
        row.AddRange(variableData);

        return row.ToArray();
    }

    [Fact]
    public void Applies_Shrinking_Modify_Row_In_Place()
    {
        var page = CreateSlottedPage([96], freeData: 200, freeCount: 1000);

        var row = BuildRow([1, 2, 3, 4], "HELLO"u8.ToArray());

        row.CopyTo(page.Data, 96);

        var record = new ModifyRowLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 0,
            OffsetInRow = 15,
            ModifySize = 5,
            BeforeData = "HELLO"u8.ToArray(),
            AfterData = "HI"u8.ToArray()
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal("HI"u8.ToArray(), page.Data[111..113]);
        Assert.Equal([96], page.OffsetTable.Select(o => (int)o));
        Assert.Equal(200, page.PageHeader.FreeData);
        Assert.Equal(1003, page.PageHeader.FreeCount);
    }

    [Fact]
    public void Applies_Growing_Modify_Row_By_Relocating_The_Row()
    {
        var page = CreateSlottedPage([96], freeData: 200, freeCount: 1000);

        var row = BuildRow([1, 2, 3, 4], "HELLO"u8.ToArray());

        row.CopyTo(page.Data, 96);

        var record = new ModifyRowLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 0,
            OffsetInRow = 15,
            ModifySize = 5,
            BeforeData = "HELLO"u8.ToArray(),
            AfterData = "WORLDWIDE"u8.ToArray()
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([200], page.OffsetTable.Select(o => (int)o));
        Assert.Equal(row[..15], page.Data[200..215]);
        Assert.Equal("WORLDWIDE"u8.ToArray(), page.Data[215..224]);
        Assert.Equal(224, page.PageHeader.FreeData);
        Assert.Equal(996, page.PageHeader.FreeCount);
    }

    [Fact]
    public void Applies_Growing_Modify_Row_In_Place_When_Row_Is_Last()
    {
        var page = CreateSlottedPage([96], freeData: 116, freeCount: 1000);

        var row = BuildRow([1, 2, 3, 4], "HELLO"u8.ToArray());

        row.CopyTo(page.Data, 96);

        var record = new ModifyRowLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 0,
            OffsetInRow = 15,
            ModifySize = 5,
            BeforeData = "HELLO"u8.ToArray(),
            AfterData = "WORLDWIDE"u8.ToArray()
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([96], page.OffsetTable.Select(o => (int)o));
        Assert.Equal("WORLDWIDE"u8.ToArray(), page.Data[111..120]);
        Assert.Equal(120, page.PageHeader.FreeData);
        Assert.Equal(996, page.PageHeader.FreeCount);
    }

    [Fact]
    public void Applies_Size_Changing_Modify_Columns_By_Rebuilding_The_Row()
    {
        var page = CreateSlottedPage([96], freeData: 200, freeCount: 1000);

        var row = BuildRow([1, 2, 3, 4], "HELLO"u8.ToArray());

        row.CopyTo(page.Data, 96);

        var record = new ModifyColumnsLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 0,
            Modifications =
            [
                new ColumnModification
                {
                    BeforeOffset = 4,
                    AfterOffset = 4,
                    BeforeLength = 2,
                    BeforeData = [1, 2],
                    AfterData = [9, 9, 9]
                },
                new ColumnModification
                {
                    BeforeOffset = 15,
                    AfterOffset = 16,
                    BeforeLength = 5,
                    BeforeData = "HELLO"u8.ToArray(),
                    AfterData = "HI"u8.ToArray()
                }
            ]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([9, 9, 9], page.Data[100..103]);
        Assert.Equal("HI"u8.ToArray(), page.Data[112..114]);
        Assert.Equal([96], page.OffsetTable.Select(o => (int)o));
        Assert.Equal(200, page.PageHeader.FreeData);
        Assert.Equal(1002, page.PageHeader.FreeCount);
    }

    [Fact]
    public void Applies_Growing_Modify_Columns_By_Relocating_The_Row()
    {
        var page = CreateSlottedPage([96], freeData: 200, freeCount: 1000);

        var row = BuildRow([1, 2, 3, 4], "HELLO"u8.ToArray());

        row.CopyTo(page.Data, 96);

        var record = new ModifyColumnsLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 0,
            Modifications =
            [
                new ColumnModification
                {
                    BeforeOffset = 15,
                    AfterOffset = 15,
                    BeforeLength = 5,
                    BeforeData = "HELLO"u8.ToArray(),
                    AfterData = "WORLDWIDE"u8.ToArray()
                }
            ]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.Applied, result.Status);
        Assert.Equal([200], page.OffsetTable.Select(o => (int)o));
        Assert.Equal(row[..15], page.Data[200..215]);
        Assert.Equal("WORLDWIDE"u8.ToArray(), page.Data[215..224]);
        Assert.Equal(224, page.PageHeader.FreeData);
        Assert.Equal(996, page.PageHeader.FreeCount);
    }

    [Fact]
    public void Rejects_Size_Changing_Modify_Row_When_Row_Structure_Is_Not_Supported()
    {
        var page = CreateSlottedPage([96], freeData: 200, freeCount: 1000);

        // Forwarding stub record type (status bits type 3)
        page.Data[96] = 0x06;

        var record = new ModifyRowLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
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
    public void Rejects_Heap_Delete_When_Row_Does_Not_Match_Row_Image()
    {
        var page = CreateSlottedPage([96, 120], freeData: 124, freeCount: 1000);

        var record = new DeleteRowsLogRecord
        {
            Lsn = RecordLsn,
            PreviousLsn = default,
            PageAddress = Address,
            PreviousPageLsn = InitialLsn,
            Context = LogContext.HEAP,
            SlotId = 1,
            RowData = [1, 2, 3, 4]
        };

        var result = LogRecordApplier.Apply(page, record);

        Assert.Equal(ApplyStatus.BeforeImageMismatch, result.Status);
        Assert.Equal([96, 120], page.OffsetTable.Select(o => (int)o));
    }
}
