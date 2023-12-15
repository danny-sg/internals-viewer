using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Services.Records.Loaders;

/// <summary>
/// Loads a Data Record object
/// </summary>
public class DataRecordLoader : RecordLoader
{
    /// <summary>
    /// Loads the specified data record.
    /// </summary>
    internal static DataRecord Load(Page page, ushort slotOffset, TableStructure structure)
    {
        var data = page.PageData;

        var dataRecord = new DataRecord();

        dataRecord.SlotOffset = slotOffset;

        dataRecord.NullBitmapSize = (short)((structure.Columns.Count(column => !column.Sparse) - 1) / 8 + 1);

        if (LoadStatusBits(dataRecord, data) == RecordType.Forwarding)
        {
            LoadForwardingRecord(dataRecord, data);

            return dataRecord;
        }

        // Fixed column offset 2-byte int located after Status Bits A (1 byte) and Status Bits B (1 byte)
        var columnCountOffsetPosition = dataRecord.SlotOffset + sizeof(byte) + sizeof(byte);

        dataRecord.ColumnCountOffset = BitConverter.ToInt16(data, columnCountOffsetPosition);

        dataRecord.MarkDataStructure("ColumnCountOffset", columnCountOffsetPosition, sizeof(short));

        // Column count 2-byte int located at the column count offset
        var columnCountPosition = dataRecord.SlotOffset + dataRecord.ColumnCountOffset;

        dataRecord.ColumnCount = BitConverter.ToInt16(data, columnCountPosition);

        dataRecord.MarkDataStructure("ColumnCount", columnCountPosition, sizeof(short));

        if (dataRecord.HasNullBitmap)
        {
            LoadNullBitmap(dataRecord, data);
        }

        short offsetStart = 0;

        if (dataRecord is { HasVariableLengthColumns: true, Compressed: false })
        {
            if (structure.HasSparseColumns
                && structure.Columns.Count(column => !column.Sparse) == 0)
            {
                dataRecord.VariableLengthColumnCount = 1;

                offsetStart = (short)(dataRecord.ColumnCountOffset + sizeof(byte) + dataRecord.NullBitmapSize);
            }
            else
            {
                // Number of variable length columns (2-byte int) located after null bitmap
                var varColCountOffset = dataRecord.ColumnCountOffset + sizeof(short) + dataRecord.NullBitmapSize;

                dataRecord.VariableLengthColumnCount = BitConverter.ToUInt16(data, dataRecord.SlotOffset + varColCountOffset);

                dataRecord.MarkDataStructure("VariableLengthColumnCount", dataRecord.SlotOffset + varColCountOffset, sizeof(short));

                // Offset starts after the variable length column count (2-bytes)
                offsetStart = (short)(varColCountOffset + sizeof(short));
            }

            // Load offset array of 2-byte ints indicating the end offset of each variable length field
            dataRecord.ColOffsetArray = GetOffsetArray(data,
                                                       dataRecord.VariableLengthColumnCount,
                                                       dataRecord.SlotOffset + offsetStart);

            dataRecord.MarkDataStructure("ColOffsetArrayDescription", 
                                         dataRecord.SlotOffset + offsetStart, 
                                         dataRecord.VariableLengthColumnCount * sizeof(short));
        }
        else
        {
            dataRecord.VariableLengthColumnCount = 0;
            dataRecord.ColOffsetArray = Array.Empty<ushort>();
        }

        // Variable length data starts after the offset array length (2 byte ints * number of variable length columns)
        dataRecord.VariableLengthDataOffset = (ushort)(offsetStart + sizeof(ushort) * dataRecord.VariableLengthColumnCount);

        LoadValues(page, dataRecord, structure);

        if (structure.HasSparseColumns)
        {
            LoadSparseVector(dataRecord, data, structure);
        }

        return dataRecord;
    }

    /// <summary>
    /// Loads the null bitmap values
    /// </summary>
    private static void LoadNullBitmap(Record dataRecord, byte[] data)
    {
        var nullBitmapBytes = new byte[dataRecord.NullBitmapSize];

        // Null bitmap located after column count offset + column count 2-byte int
        var nullBitmapPosition = dataRecord.SlotOffset + dataRecord.ColumnCountOffset + sizeof(short);

        Array.Copy(data,
                   nullBitmapPosition,
                   nullBitmapBytes,
                   0,
                   dataRecord.NullBitmapSize);

        dataRecord.NullBitmap = new BitArray(nullBitmapBytes);

        dataRecord.MarkDataStructure("NullBitmapDescription", nullBitmapPosition, dataRecord.NullBitmapSize);
    }

    /// <summary>
    /// Loads the sparse vector.
    /// </summary>
    private static void LoadSparseVector(DataRecord dataRecord, byte[] data, TableStructure structure)
    {
        int startOffset;

        if (dataRecord.ColOffsetArray.Length == 1)
        {
            startOffset = dataRecord.VariableLengthDataOffset;
        }
        else
        {
            startOffset = dataRecord.ColOffsetArray[^2];
        }

        int endOffset = DecodeOffset(dataRecord.ColOffsetArray[^1]);

        var sparseRecord = new byte[endOffset - startOffset];

        Array.Copy(data, dataRecord.SlotOffset + startOffset, sparseRecord, 0, endOffset - startOffset);

        dataRecord.MarkDataStructure("SparseVector");

        dataRecord.SparseVector = new SparseVector(sparseRecord, structure, dataRecord, (short)startOffset);
    }

    /// <summary>
    /// Loads the column values.
    /// </summary>
    private static void LoadValues(Page page, DataRecord dataRecord, TableStructure structure)
    {
        var columnValues = new List<RecordField>();

        var index = 0;

        foreach (var column in structure.Columns)
        {
            if (!column.Sparse)
            {
                var field = new RecordField(column);

                short length = 0;
                ushort offset = 0;
                var isLob = false;
                var data = Array.Empty<byte>();

                ushort variableIndex = 0;

                if (column.LeafOffset is >= 0 and < Page.Size)
                {
                    // Fixed length field

                    // Fixed offset given by the column leaf offset field in sys.columns
                    offset = (ushort)column.LeafOffset;

                    // Length fixed from data type/data length
                    length = column.DataLength;

                    data = new byte[length];

                    Array.Copy(page.PageData, column.LeafOffset + dataRecord.SlotOffset, data, 0, length);
                }
                else if (dataRecord is { HasVariableLengthColumns: true, HasNullBitmap: true } && !column.Dropped
                         && (column.ColumnId < 0 || !dataRecord.NullBitmapValue(column)))
                {
                    // Variable Length fields

                    // Use the leaf offset to get the variable length column ordinal
                    variableIndex = (ushort)(column.LeafOffset * -1 - 1);

                    if (variableIndex == 0)
                    {
                        // If it's position 0 the start of the data will be at the variable length data offset...
                        offset = dataRecord.VariableLengthDataOffset;
                    }
                    else
                    {
                        // ...else use the end offset of the previous column as the start of this one
                        offset = DecodeOffset(dataRecord.ColOffsetArray[variableIndex - 1]);
                    }

                    if (variableIndex < dataRecord.ColOffsetArray.Length)
                    {
                        isLob = (dataRecord.ColOffsetArray[variableIndex] & 0x8000) == 0x8000;
                        length = (short)(DecodeOffset(dataRecord.ColOffsetArray[variableIndex]) - offset);
                    }
                    else
                    {
                        isLob = false;
                        length = 0;
                    }

                    data = new byte[length];

                    Array.Copy(page.PageData, dataRecord.SlotOffset + offset, data, 0, length);
                }

                field.Offset = offset;
                field.Length = length;
                field.Data = data;
                field.VariableOffset = variableIndex;

                if (isLob)
                {
                    LoadLobField(field, data, dataRecord.SlotOffset + offset);
                }
                else
                {
                    field.MarkDataStructure("Value", dataRecord.SlotOffset + field.Offset, field.Length);
                }

                dataRecord.MarkDataStructure("FieldsArray", field.Name, index);

                index++;

                columnValues.Add(field);
            }
        }

        dataRecord.Fields.AddRange(columnValues);
    }

    /// <summary>
    /// Loads the status bits.
    /// </summary>
    private static RecordType LoadStatusBits(Record record, byte[] data)
    {
        var statusA = data[record.SlotOffset];

        // bytes 0 and 1 are Status Bits A and B
        record.StatusBitsA = new BitArray(new[] { statusA });
        record.StatusBitsB = new BitArray(new[] { data[record.SlotOffset + 1] });

        record.MarkDataStructure("StatusBitsADescription", record.SlotOffset, sizeof(byte));

        record.RecordType = (RecordType)(statusA >> 1 & 7);

        if (record.RecordType == RecordType.Forwarding)
        {
            return record.RecordType;
        }

        record.HasNullBitmap = record.StatusBitsA[4];
        record.HasVariableLengthColumns = record.StatusBitsA[5];

        record.MarkDataStructure("StatusBitsBDescription", record.SlotOffset + sizeof(byte), sizeof(byte));

        return record.RecordType;
    }

    /// <summary>
    /// Loads a forwarding record.
    /// </summary>
    private static void LoadForwardingRecord(DataRecord dataRecord, byte[] data)
    {
        var forwardingRecord = new byte[8];

        Array.Copy(data, dataRecord.SlotOffset + sizeof(byte), forwardingRecord, 0, 6);

        dataRecord.ForwardingRecord = new RowIdentifier(forwardingRecord);

        dataRecord.MarkDataStructure("ForwardingRecord", dataRecord.SlotOffset + sizeof(byte), 6);
    }
}