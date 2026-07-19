using System.Buffers.Binary;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Query.TransactionLog.LogRecords;

namespace InternalsViewer.Query.TransactionLog;

internal static class LogRecordParser
{
    private const int HeaderSize = 24;

    private const int FixedLengthOffset = 2;
    private const int PreviousLsnOffset = 4;
    private const int TransactionIdOffset = 16;
    private const int OperationOffset = 22;
    private const int ContextOffset = 23;

    private const int PageIdOffset = 24;
    private const int FileIdOffset = 28;
    private const int SlotIdOffset = 30;
    private const int PreviousPageLsnOffset = 36;
    private const int PartitionIdOffset = 48;
    private const int OffsetInRowOffset = 56;
    private const int ModifySizeOffset = 58;

    private const int FirstBitOffset = 48;
    private const int BitCountOffset = 50;
    private const int BitValueOffset = 52;

    private const int PageOffsetOffset = 48;
    private const int NewValueOffset = 50;
    private const int OldValueOffset = 51;

    private const int PageTypeOffset = 48;
    private const int PageLevelOffset = 50;
    private const int FormatOptionOffset = 51;
    private const int PageStatOffset = 66;

    private const int BeginTimeOffset = 40;
    private const int EndTimeOffset = 24;

    private static readonly DateTime BaseDateTime = new(1900, 1, 1);

    public static LogRecord Parse(LogSequenceNumber lsn, byte[] data)
    {
        if (data.Length < HeaderSize)
        {
            throw new ArgumentException($"Log record must be at least {HeaderSize} bytes", nameof(data));
        }

        var span = data.AsSpan();

        var fixedLength = BinaryPrimitives.ReadUInt16LittleEndian(span[FixedLengthOffset..]);

        var operation = (LogOperation)span[OperationOffset];

        var context = (LogContext)span[ContextOffset];

        var transactionIdLow = BinaryPrimitives.ReadUInt32LittleEndian(span[TransactionIdOffset..]);

        var transactionIdHigh = BinaryPrimitives.ReadUInt16LittleEndian(span[(TransactionIdOffset + sizeof(uint))..]);

        var previousLsn = LogSequenceNumberParser.Parse(span[PreviousLsnOffset..]);

        var record = CreateRecord(operation, span, lsn, previousLsn);

        record.LogTransactionId = $"{transactionIdHigh:x4}:{transactionIdLow:x8}";
        record.TransactionId = ((long)transactionIdHigh << 32) | transactionIdLow;
        record.Operation = operation;
        record.Context = context;
        record.LogRecordSize = (short)fixedLength;

        return record;
    }

    private static LogRecord CreateRecord(LogOperation operation,
                                          ReadOnlySpan<byte> span,
                                          LogSequenceNumber lsn,
                                          LogSequenceNumber previousLsn)
    {
        switch (operation)
        {
            case LogOperation.LOP_BEGIN_XACT:
                return ParseBeginTransaction(span, lsn, previousLsn);

            case LogOperation.LOP_COMMIT_XACT:
            case LogOperation.LOP_ABORT_XACT:
                return new EndTransactionLogRecord
                {
                    Lsn = lsn,
                    PreviousLsn = previousLsn,
                    EndTime = ParseDateTime(span[EndTimeOffset..])
                };

            case LogOperation.LOP_INSERT_ROWS:
                return ParseInsertRows(span, lsn, previousLsn);

            case LogOperation.LOP_DELETE_ROWS:
                return ParseDeleteRows(span, lsn, previousLsn);

            case LogOperation.LOP_MODIFY_ROW:
                return ParseModifyRow(span, lsn, previousLsn);

            case LogOperation.LOP_MODIFY_COLUMNS:
                return ParseModifyColumns(span, lsn, previousLsn);

            case LogOperation.LOP_FORMAT_PAGE:
                return new FormatPageLogRecord
                {
                    Lsn = lsn,
                    PreviousLsn = previousLsn,
                    PageAddress = ParsePageAddress(span),
                    SlotId = BinaryPrimitives.ReadInt16LittleEndian(span[SlotIdOffset..]),
                    PreviousPageLsn = LogSequenceNumberParser.Parse(span[PreviousPageLsnOffset..]),
                    PageType = BinaryPrimitives.ReadUInt16LittleEndian(span[PageTypeOffset..]),
                    PageLevel = span[PageLevelOffset],
                    FormatOption = span[FormatOptionOffset],
                    PageStat = BinaryPrimitives.ReadUInt16LittleEndian(span[PageStatOffset..])
                };

            case LogOperation.LOP_SET_BITS:
                return new SetBitsLogRecord
                {
                    Lsn = lsn,
                    PreviousLsn = previousLsn,
                    PageAddress = ParsePageAddress(span),
                    SlotId = BinaryPrimitives.ReadUInt16LittleEndian(span[SlotIdOffset..]),
                    PreviousPageLsn = LogSequenceNumberParser.Parse(span[PreviousPageLsnOffset..]),
                    FirstBit = BinaryPrimitives.ReadUInt16LittleEndian(span[FirstBitOffset..]),
                    BitCount = BinaryPrimitives.ReadUInt16LittleEndian(span[BitCountOffset..]),
                    BitValue = BinaryPrimitives.ReadUInt16LittleEndian(span[BitValueOffset..])
                };

            case LogOperation.LOP_SET_FREE_SPACE:
                return new SetFreeSpaceLogRecord
                {
                    Lsn = lsn,
                    PreviousLsn = previousLsn,
                    PageAddress = ParsePageAddress(span),
                    SlotId = BinaryPrimitives.ReadUInt16LittleEndian(span[SlotIdOffset..]),
                    PreviousPageLsn = LogSequenceNumberParser.Parse(span[PreviousPageLsnOffset..]),
                    PageOffset = BinaryPrimitives.ReadUInt16LittleEndian(span[PageOffsetOffset..]),
                    NewValue = span[NewValueOffset],
                    OldValue = span[OldValueOffset]
                };

            default:
                return new LogRecord { Lsn = lsn, PreviousLsn = previousLsn };
        }
    }

    private static BeginTransactionLogRecord ParseBeginTransaction(ReadOnlySpan<byte> span,
                                                                   LogSequenceNumber lsn,
                                                                   LogSequenceNumber previousLsn)
    {
        var contents = ParseVariableContents(span);

        return new BeginTransactionLogRecord
        {
            Lsn = lsn,
            PreviousLsn = previousLsn,
            BeginTime = ParseDateTime(span[BeginTimeOffset..]),
            TransactionName = Encoding.Unicode.GetString(GetElement(contents, 0)),
            TransactionSid = GetElement(contents, 1)
        };
    }

    private static InsertRowsLogRecord ParseInsertRows(ReadOnlySpan<byte> span,
                                                       LogSequenceNumber lsn,
                                                       LogSequenceNumber previousLsn)
    {
        var contents = ParseVariableContents(span);

        return new InsertRowsLogRecord
        {
            Lsn = lsn,
            PreviousLsn = previousLsn,
            PageAddress = ParsePageAddress(span),
            SlotId = BinaryPrimitives.ReadUInt16LittleEndian(span[SlotIdOffset..]),
            PreviousPageLsn = LogSequenceNumberParser.Parse(span[PreviousPageLsnOffset..]),
            PartitionId = BinaryPrimitives.ReadInt64LittleEndian(span[PartitionIdOffset..]),
            NumElements = contents.Length,
            RowData = GetElement(contents, 0)
        };
    }

    private static DeleteRowsLogRecord ParseDeleteRows(ReadOnlySpan<byte> span,
                                                       LogSequenceNumber lsn,
                                                       LogSequenceNumber previousLsn)
    {
        var contents = ParseVariableContents(span);

        return new DeleteRowsLogRecord
        {
            Lsn = lsn,
            PreviousLsn = previousLsn,
            PageAddress = ParsePageAddress(span),
            SlotId = BinaryPrimitives.ReadUInt16LittleEndian(span[SlotIdOffset..]),
            PreviousPageLsn = LogSequenceNumberParser.Parse(span[PreviousPageLsnOffset..]),
            PartitionId = BinaryPrimitives.ReadInt64LittleEndian(span[PartitionIdOffset..]),
            NumElements = contents.Length,
            RowData = GetElement(contents, 0)
        };
    }

    private static ModifyRowLogRecord ParseModifyRow(ReadOnlySpan<byte> span,
                                                     LogSequenceNumber lsn,
                                                     LogSequenceNumber previousLsn)
    {
        var contents = ParseVariableContents(span);

        return new ModifyRowLogRecord
        {
            Lsn = lsn,
            PreviousLsn = previousLsn,
            PageAddress = ParsePageAddress(span),
            SlotId = BinaryPrimitives.ReadUInt16LittleEndian(span[SlotIdOffset..]),
            PreviousPageLsn = LogSequenceNumberParser.Parse(span[PreviousPageLsnOffset..]),
            PartitionId = BinaryPrimitives.ReadInt64LittleEndian(span[PartitionIdOffset..]),
            NumElements = contents.Length,
            OffsetInRow = BinaryPrimitives.ReadUInt16LittleEndian(span[OffsetInRowOffset..]),
            ModifySize = BinaryPrimitives.ReadUInt16LittleEndian(span[ModifySizeOffset..]),
            BeforeData = GetElement(contents, 0),
            AfterData = GetElement(contents, 1)
        };
    }

    private static ModifyColumnsLogRecord ParseModifyColumns(ReadOnlySpan<byte> span,
                                                             LogSequenceNumber lsn,
                                                             LogSequenceNumber previousLsn)
    {
        var contents = ParseVariableContents(span);

        var offsetPairs = GetElement(contents, 0);

        var regionCount = offsetPairs.Length / (2 * sizeof(ushort));

        var modifications = new List<ColumnModification>(regionCount);

        for (var i = 0; i < regionCount; i++)
        {
            modifications.Add(new ColumnModification
            {
                BeforeOffset = BinaryPrimitives.ReadUInt16LittleEndian(offsetPairs.AsSpan(i * 2 * sizeof(ushort))),
                AfterOffset = BinaryPrimitives.ReadUInt16LittleEndian(offsetPairs.AsSpan(i * 2 * sizeof(ushort) + 2)),
                BeforeData = GetElement(contents, 4 + i * 2),
                AfterData = GetElement(contents, 4 + i * 2 + 1)
            });
        }

        return new ModifyColumnsLogRecord
        {
            Lsn = lsn,
            PreviousLsn = previousLsn,
            PageAddress = ParsePageAddress(span),
            SlotId = BinaryPrimitives.ReadUInt16LittleEndian(span[SlotIdOffset..]),
            PreviousPageLsn = LogSequenceNumberParser.Parse(span[PreviousPageLsnOffset..]),
            PartitionId = BinaryPrimitives.ReadInt64LittleEndian(span[PartitionIdOffset..]),
            NumElements = contents.Length,
            Modifications = modifications
        };
    }

    private static PageAddress ParsePageAddress(ReadOnlySpan<byte> span)
    {
        return new PageAddress(BinaryPrimitives.ReadInt16LittleEndian(span[FileIdOffset..]),
                               BinaryPrimitives.ReadInt32LittleEndian(span[PageIdOffset..]));
    }

    private static byte[][] ParseVariableContents(ReadOnlySpan<byte> span)
    {
        var fixedLength = BinaryPrimitives.ReadUInt16LittleEndian(span[FixedLengthOffset..]);

        if (span.Length < fixedLength + sizeof(ushort))
        {
            return [];
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(span[fixedLength..]);

        var lengthArrayOffset = fixedLength + sizeof(ushort);

        if (span.Length < lengthArrayOffset + count * sizeof(ushort))
        {
            return [];
        }

        var contents = new byte[count][];

        var dataOffset = AlignToDword(lengthArrayOffset + count * sizeof(ushort));

        for (var i = 0; i < count; i++)
        {
            var length = BinaryPrimitives.ReadUInt16LittleEndian(span[(lengthArrayOffset + i * sizeof(ushort))..]);

            var available = Math.Clamp(span.Length - dataOffset, 0, length);

            contents[i] = span.Slice(dataOffset, available).ToArray();

            dataOffset = AlignToDword(dataOffset + length);
        }

        return contents;
    }

    private static byte[] GetElement(byte[][] contents, int index)
    {
        return index < contents.Length ? contents[index] : [];
    }

    private static DateTime ParseDateTime(ReadOnlySpan<byte> span)
    {
        var time = BinaryPrimitives.ReadUInt32LittleEndian(span);

        var days = BinaryPrimitives.ReadInt32LittleEndian(span[sizeof(uint)..]);

        return BaseDateTime.AddDays(days).AddTicks(time * TimeSpan.TicksPerSecond / 300);
    }

    private static int AlignToDword(int offset)
    {
        return (offset + 3) & ~3;
    }
}
