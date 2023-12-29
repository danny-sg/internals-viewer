using System.Collections;
using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Services.Loaders.Records;

/// <summary>
/// Loads a Compressed Data Record
/// </summary>
public class CompressedDataRecordService : RecordLoader, ICompressedDataRecordService
{
    public CompressedDataRecord Load(AllocationUnitPage page, ushort slotOffset, Structure structure)
    {
        var data = page.Data;

        var record = new CompressedDataRecord();

        record.ColumnCount = LoadNumberOfColumns(record, data);

        LoadStatus(record, data);

        if (record.RecordType == RecordType.ForwardingStub)
        {
            return LoadForwardingRecord(record);
        }

        var cdArraySize = record.ColumnCount / 2 + record.ColumnCount % 2;

        LoadCdArray(record, data);

        record.CompressedSize = 0;

        for (var i = 0; i < record.ColumnCount; i++)
        {
            record.CompressedSize += Convert.ToInt16(GetCdArrayItemSize(record.GetCdByte(i)));
        }

        if (record.HasVariableLengthColumns)
        {
            //record.Unknown = record.Page.PageData[record.SlotOffset + 1 + record.ColumnCountBytes + cdArraySize + record.CompressedSize];

            var varLengthColumnCountOffset = record.SlotOffset + 2 + record.ColumnCountBytes + cdArraySize + record.CompressedSize;

            record.VariableLengthColumnCount = BitConverter.ToUInt16(data, varLengthColumnCountOffset);

            record.MarkDataStructure("VariableLengthColumnCount", varLengthColumnCountOffset, sizeof(short));
        }

        LoadShortFields(record, page, structure);

        if (record is { VariableLengthColumnCount: > 0, HasVariableLengthColumns: true })
        {
            var colArrayOffset = record.SlotOffset + (5 + cdArraySize) + record.CompressedSize;

            record.ColOffsetArray = GetOffsetArray(data,
                                                   record.VariableLengthColumnCount,
                                                   colArrayOffset);

            record.MarkDataStructure("ColOffsetArrayDescription", colArrayOffset, record.VariableLengthColumnCount * sizeof(short));
        }

        var longStartPosition = record.SlotOffset + 4 + record.ColumnCountBytes + cdArraySize + record.CompressedSize + 2 * record.VariableLengthColumnCount;

        LoadLongFields(longStartPosition, record, page, structure);

        return record;
    }

    internal static void LoadStatus(CompressedDataRecord record, byte[] data)
    {
        record.StatusBitsA = new BitArray(new[] { data[record.SlotOffset] });

        record.RecordType = (RecordType)(data[record.SlotOffset] >> 1 & 7);

        record.HasVariableLengthColumns = record.StatusBitsA[5];
        record.HasNullBitmap = record.StatusBitsA[4];

        record.MarkDataStructure("StatusBitsADescription", record.SlotOffset, sizeof(byte));
    }

    private CompressedDataRecord LoadForwardingRecord(CompressedDataRecord record)
    {
        throw new NotImplementedException();
    }

    internal static short LoadNumberOfColumns(CompressedDataRecord record, byte[] data)
    {
        short columns;

        if ((data[record.SlotOffset + 1] & 0x80) == 0x80)
        {
            // Check if the first bit is high, if it is it indicates 2-byte int
            record.ColumnCountBytes = 2;

            var noOfColumnsData = new byte[2];

            Array.Copy(data, record.SlotOffset + 1, noOfColumnsData, 0, 2);

            noOfColumnsData[0] = Convert.ToByte(noOfColumnsData[0] ^ 0x80);

            Array.Reverse(noOfColumnsData);

            columns = BitConverter.ToInt16(noOfColumnsData, 0);
        }
        else
        {
            record.ColumnCountBytes = 1;

            columns = data[record.SlotOffset + 1];
        }

        // 1/2 byte int located after the status bits (1 byte)
        record.MarkDataStructure("ColumnCount", record.SlotOffset + sizeof(byte), record.ColumnCountBytes);

        return columns;
    }

    public static void LoadShortFields(CompressedDataRecord record, AllocationUnitPage page, Structure structure)
    {
        LoadShortFields(record, false, page, structure);
    }

    public static void LoadShortFields(CompressedDataRecord record,
                                       bool hasDownPagePointer,
                                       AllocationUnitPage page,
                                       Structure structure)
    {
        var index = 0;

        var offset = record.SlotOffset + 1 + record.ColumnCountBytes + record.ColumnCount / 2 + record.ColumnCount % 2;

        for (var i = 0; i < record.ColumnCount; i++)
        {
            if (record.GetCdByte(i) != 10)
            {
                CompressedRecordField field;

                field = new CompressedRecordField(structure.Columns[i], record);
                field.Compressed = true;

                if (field.ColumnStructure.DataType == SqlDbType.Bit)
                {
                    field.Length = 1;
                    field.Compressed = true;
                    field.Data = new[] { record.GetCdByte(i) };
                }
                else
                {
                    var size = GetCdArrayItemSize(record.GetCdByte(i));

                    field.IsNull = record.GetCdByte(i) == 0;
                    field.Length = size;

                    field.PageSymbol = record.GetCdByte(i) > 10;

                    if (size > 0)
                    {
                        field.Data = new byte[size];
                        Array.Copy(page.Data, offset, field.Data, 0, size);

                        field.Offset = offset;
                        offset += size;
                    }
                }

                if (page.CompressionInfo is { AnchorRecord: not null })
                {
                    field.AnchorField = page.CompressionInfo.AnchorRecord.Fields.First(f => f.ColumnStructure.ColumnId == i + 1);
                }

                field.MarkDataStructure("Value", field.Offset, field.Length);

                record.MarkDataStructure("FieldsArray", field.Name, index);

                index++;

                record.Fields.Add(field);
            }
        }
    }

    public static void LoadLongFields(int startPosition,
                                      CompressedDataRecord record,
                                      AllocationUnitPage page,
                                      Structure structure)
    {
        var longColIndex = 0;
        var prevLength = 0;

        for (var i = 0; i < record.ColumnCount; i++)
        {
            if (record.GetCdByte(i) == 10)
            {
                var field = new CompressedRecordField(structure.Columns[i], record);

                field.Length = DecodeOffset(record.ColOffsetArray[longColIndex]) - prevLength;
                field.Data = new byte[field.Length];
                field.Offset = startPosition + prevLength;

                var isLob = (record.ColOffsetArray[longColIndex] & 0x8000) == 0x8000;

                Array.Copy(page.Data, field.Offset, field.Data, 0, field.Length);

                if (page.CompressionInfo is { AnchorRecord: not null })
                {
                    field.AnchorField = page.CompressionInfo.AnchorRecord.Fields.First(f => f.ColumnStructure.ColumnId == i);
                }

                record.Fields.Add(field);

                if (isLob)
                {
                    LoadLobField(field, field.Data, field.Offset);
                }
                else
                {
                    field.MarkDataStructure("Value", field.Offset, field.Length);
                }

                record.MarkDataStructure("FieldsArray", field.Name, record.Fields.Count - 1);

                prevLength = DecodeOffset(record.ColOffsetArray[longColIndex]);

                longColIndex++;
            }
        }
    }

    public static int GetCdArrayItemSize(int cdItem)
    {
        if (cdItem is > 0 and < 10)
        {
            return cdItem - 1;
        }

        if (cdItem > 10)
        {
            return cdItem - 11;
        }

        return 0;
    }

    /// <summary>
    /// Loads the CD (column descriptor) Array.
    /// </summary>
    private static void LoadCdArray(CompressedDataRecord record, byte[] data)
    {
        var bytePosition = 1 + record.ColumnCountBytes;

        for (var i = 0; i < Math.Ceiling(record.ColumnCount / 2D); i++)
        {
            record.MarkDataStructure("CdItemsArray", "CD Array offset " + i, i);

            var cdItem = Convert.ToByte(data[record.SlotOffset + bytePosition]);

            var item = new CdArray(i, cdItem);

            item.MarkDataStructure("Description", record.SlotOffset + bytePosition, sizeof(byte));

            record.CdItems.Add(item);

            bytePosition++;
        }
    }
}