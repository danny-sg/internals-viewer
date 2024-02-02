using System.Collections;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Metadata.Helpers;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Services.Loaders.Records;

/// <summary>
/// Loads a Data Record using a combination of the table structure and the record structure
/// </summary>
/// <remarks>
/// Microsoft SQL Server 2012 Internals by Kalen Delaney et al. has a good description of the data record structure in Chapter 6.
/// </remarks>
public class FixedVarRecord(ILogger<FixedVarRecord> logger) : FixedVarRecordLoader
{
    private ILogger<FixedVarRecord> Logger { get; } = logger;

    /// <summary>
    /// Loads the data record at the given offset
    /// </summary>
    public DataRecord Load(DataPage page, ushort slotOffset, TableStructure structure)
    {
        var data = page.Data;

        Logger.BeginScope("Data Record Loader: {FileId}{PageId}:{Offset}", page.PageAddress.FileId, page.PageAddress.PageId, slotOffset);

        Logger.LogTrace(structure.ToDetailString());

        var record = new DataRecord
        {
            Offset = slotOffset,
            RowIdentifier = new RowIdentifier(page.PageAddress, slotOffset)
        };

        // Records will always have Status Bits A
        LoadStatusBitsA(record, data);

        // If the Record Type defined in status Bits A is a forwarding stub the rest of the record is the stub
        if (record.RecordType == RecordType.ForwardingStub)
        {
            LoadForwardingStub(record, data);

            return record;
        }

        // Non-forwarding stub records will always have Status Bits B
        LoadStatusBitsB(record, data);

        // The record structure gives information about the structure of the record to decode the values
        LoadRecordStructure(record, data, structure);

        LoadValues(record, structure, data);

        if (structure.HasSparseColumns)
        {
            LoadSparseVector(record, structure, data);
        }

        return record;
    }

    /// <summary>
    /// Load the record structure
    /// </summary>
    /// <remarks>
    /// Record structure is as follows:
    /// 
    ///     Status Bits A                              - 1 byte
    ///     Status Bits B                              - 1 byte
    ///     Fixed length data size/column count offset - 2 bytes
    ///     Fixed length data                          - defined by Fixed length data size
    ///     Number of columns                          - 2 bytes
    ///     NULL bitmap                                - Ceiling(Number of non-sparse columns / 8) bytes
    ///     
    ///     If Status Bits A Bit 5 (Has Variable Length Columns) is set:
    ///     
    ///     Number of variable length columns          - 2 bytes
    ///     Column offset array                        - 2 bytes * Number of variable length columns
    ///     Variable length data                       - defined by Column offset array
    /// </remarks>
    private static void LoadRecordStructure(DataRecord dataRecord, byte[] data, TableStructure structure)
    {
        // Fixed column offset 2-byte int located after Status Bits A (1 byte) and Status Bits B (1 byte)
        var columnCountOffsetPosition = dataRecord.Offset + sizeof(byte) + sizeof(byte);

        // Column count offset determines the offset of the column count after the fixed length data
        dataRecord.ColumnCountOffset = BitConverter.ToInt16(data, columnCountOffsetPosition);

        dataRecord.MarkProperty(nameof(DataRecord.ColumnCountOffset), columnCountOffsetPosition, sizeof(short));

        // Column count 2-byte integer located at the column count offset
        var columnCountPosition = dataRecord.Offset + dataRecord.ColumnCountOffset;

        dataRecord.ColumnCount = BitConverter.ToInt16(data, columnCountPosition);

        dataRecord.MarkProperty(nameof(DataRecord.ColumnCount), columnCountPosition, sizeof(short));

        // Calculate the number of bytes required to store the null bitmap for each non-sparse column, rounded up to the nearest byte
        dataRecord.NullBitmapSize = (short)Math.Ceiling(structure.Columns.Count(column => !column.IsSparse) / 8.0);

        // Has Null Bitmap defined by Status Bits A Bit 4
        if (dataRecord.HasNullBitmap)
        {
            LoadNullBitmap(dataRecord, data);
        }

        if (dataRecord.HasVariableLengthColumns)
        {
            short offsetStart;

            if (structure.HasSparseColumns
                && structure.Columns.Count(column => !column.IsSparse) == 0)
            {
                dataRecord.VariableLengthColumnCount = 1;

                offsetStart = (short)(dataRecord.ColumnCountOffset + sizeof(byte) + dataRecord.NullBitmapSize);
            }
            else
            {
                // Number of variable length columns (2-byte int) located after null bitmap
                var variableLengthColumnCountOffset = dataRecord.ColumnCountOffset + sizeof(short) + dataRecord.NullBitmapSize;

                dataRecord.VariableLengthColumnCount = BitConverter.ToUInt16(data,
                                                                             dataRecord.Offset + variableLengthColumnCountOffset);

                dataRecord.MarkProperty(nameof(DataRecord.VariableLengthColumnCount),
                                        dataRecord.Offset + variableLengthColumnCountOffset,
                                        sizeof(short));

                // Offset starts after the variable length column count (2-bytes)
                offsetStart = (short)(variableLengthColumnCountOffset + sizeof(short));
            }

            // Load offset array of 2-byte integers indicating the end offset of each variable length field
            dataRecord.VariableLengthColumnOffsetArray = RecordHelpers.GetOffsetArray(data,
                                                                     dataRecord.VariableLengthColumnCount,
                                                                     dataRecord.Offset + offsetStart);

            dataRecord.MarkProperty(nameof(DataRecord.VariableLengthColumnOffsetArray),
                                    dataRecord.Offset + offsetStart,
                                    dataRecord.VariableLengthColumnCount * sizeof(short));

            // Variable length data starts after the offset array length (2 byte integers * number of variable length columns)
            dataRecord.VariableLengthDataOffset = (ushort)(offsetStart + sizeof(ushort) * dataRecord.VariableLengthColumnCount);
        }
        else
        {
            dataRecord.VariableLengthColumnCount = 0;
            dataRecord.VariableLengthColumnOffsetArray = Array.Empty<ushort>();
        }
    }

    /// <summary>
    /// Loads the null bitmap values
    /// </summary>
    private static void LoadNullBitmap(DataRecord dataRecord, byte[] data)
    {
        var nullBitmapBytes = new byte[dataRecord.NullBitmapSize];

        // Null bitmap located after column count offset + column count 2-byte int
        var nullBitmapPosition = dataRecord.Offset + dataRecord.ColumnCountOffset + sizeof(short);

        Array.Copy(data,
                   nullBitmapPosition,
                   nullBitmapBytes,
                   0,
                   dataRecord.NullBitmapSize);

        dataRecord.NullBitmap = new BitArray(nullBitmapBytes);

        dataRecord.MarkProperty(nameof(DataRecord.NullBitmap), nullBitmapPosition, dataRecord.NullBitmapSize);
    }

    /// <summary>
    /// Loads the sparse vector.
    /// </summary>
    private static void LoadSparseVector(DataRecord dataRecord, TableStructure structure, byte[] pageData)
    {
        int startOffset;

        if (dataRecord.VariableLengthColumnOffsetArray.Length == 1)
        {
            startOffset = dataRecord.VariableLengthDataOffset;
        }
        else
        {
            startOffset = dataRecord.VariableLengthColumnOffsetArray[^2];
        }

        int endOffset = RecordHelpers.DecodeOffset(dataRecord.VariableLengthColumnOffsetArray[^1]);

        var sparseRecord = new byte[endOffset - startOffset];

        Array.Copy(pageData, dataRecord.Offset + startOffset, sparseRecord, 0, endOffset - startOffset);

        dataRecord.MarkProperty(nameof(DataRecord.SparseVector));

        dataRecord.SparseVector = new SparseVector(sparseRecord, structure, dataRecord, (short)startOffset);
    }

    /// <summary>
    /// Loads the column values.
    /// </summary>
    private void LoadValues(DataRecord dataRecord, TableStructure structure, byte[] pageData)
    {
        var columnValues = new List<FixedVarRecordField>();

        var nullBitmapOffset = 0;

        foreach (var column in structure.Columns)
        {
            if (!column.IsSparse)
            {
                FixedVarRecordField field;

                if (column.LeafOffset is >= 0 and < PageData.Size && !dataRecord.IsNullBitmapSet(column, nullBitmapOffset))
                {
                    // Fixed length field
                    field = LoadFixedLengthField(column, dataRecord, pageData);
                }
                else if (dataRecord is { HasVariableLengthColumns: true, HasNullBitmap: true }
                         && !column.IsDropped
                         && (column.ColumnId < 0 || !dataRecord.IsNullBitmapSet(column, nullBitmapOffset)))
                {
                    // Variable length field
                    field = LoadVariableLengthField(column, dataRecord, pageData);
                }
                else
                {
                    if (SqlTypeHelpers.IsVariableLength(column.DataType) && !dataRecord.HasVariableLengthColumns)
                    {
                        // Sees to be a case where instead of the null bitmap a field is null via the existence of a variable length column
                        // with the absence of a variable length record flag. In this case the null bitmap needs offsetting.
                        nullBitmapOffset -= 1;
                    }

                    // Null bitmap set
                    field = LoadNullField(column);
                }

                columnValues.Add(field);
            }
        }

        dataRecord.Fields.AddRange(columnValues);
    }

    private static FixedVarRecordField LoadNullField(ColumnStructure column)
    {
        var nullField = new FixedVarRecordField(column);

        nullField.MarkProperty(nameof(FixedVarRecordField.Value));

        return nullField;
    }

    /// <summary>
    /// Loads Fixed Length Fields into a new Record Field
    /// </summary>
    /// <remarks>
    /// Fixed length fields are based on the length of the field defined in the table structure.
    /// </remarks>
    private FixedVarRecordField LoadFixedLengthField(ColumnStructure column, Record dataRecord, byte[] pageData)
    {
        var field = new FixedVarRecordField(column);

        // Fixed offset given by the column leaf offset field in sys.columns
        var offset = column.LeafOffset;

        // Length fixed from data type/data length
        var length = column.DataLength;

        var data = new byte[length];

        Array.Copy(pageData, column.LeafOffset + dataRecord.Offset, data, 0, length);

        field.Offset = offset;
        field.Length = length;
        field.Data = data;

        dataRecord.MarkValue(ItemType.FixedLengthValue, column.ColumnName, field, dataRecord.Offset + field.Offset, field.Length);

        return field;
    }

    /// <summary>
    /// Loads Variable Length Fields into a new Record Field
    /// </summary>
    /// <remarks>
    /// Variable length fields are based on the offset array in the record structure.
    /// 
    /// The offset array is used to work out the start and end of each variable length field.
    /// 
    /// If the first bit is set in the offset array entry, the field is a LOB field. Instead of the value the data will be a pointer to 
    /// the LOB root.
    /// </remarks>
    private FixedVarRecordField LoadVariableLengthField(ColumnStructure column, DataRecord dataRecord, byte[] pageData)
    {
        var field = new FixedVarRecordField(column);

        short length;
        ushort offset;
        bool isLob;

        // Use the leaf offset to get the variable length column ordinal
        var fieldIndex = Math.Abs(column.LeafOffset) - 1;

        if (fieldIndex == 0)
        {
            // If position 0 the start of the data will be at the variable length data offset...
            offset = dataRecord.VariableLengthDataOffset;
        }
        else
        {
            // ...else use the end offset of the previous column as the start of this one
            offset = RecordHelpers.DecodeOffset(dataRecord.VariableLengthColumnOffsetArray[fieldIndex - 1]);
        }

        if (fieldIndex < dataRecord.VariableLengthColumnOffsetArray.Length)
        {
            // LOB field is indicated by the first/high bit being set in the offset entry (0x8000 = 32768 = 0b1000000000000000)
            isLob = (dataRecord.VariableLengthColumnOffsetArray[fieldIndex] & 0x8000) == 0x8000;

            length = (short)(RecordHelpers.DecodeOffset(dataRecord.VariableLengthColumnOffsetArray[fieldIndex]) - offset);
        }
        else
        {
            isLob = false;
            length = 0;
        }

        var data = new byte[length];

        Array.Copy(pageData, dataRecord.Offset + offset, data, 0, length);

        field.Offset = offset;
        field.Length = length;
        field.Data = data;
        field.VariableOffset = fieldIndex;

        if (isLob)
        {
            LoadLobField(field, data, dataRecord.Offset + offset);
        }
        else
        {
            dataRecord.MarkValue(ItemType.VariableLengthValue, 
                                 column.ColumnName, 
                                 field, 
                                 dataRecord.Offset + field.Offset, 
                                 field.Length);
        }

        return field;
    }

    /// <summary>
    /// Loads Status bits B
    /// </summary>
    private void LoadStatusBitsB(DataRecord record, byte[] data)
    {
        record.StatusBitsB = data[record.Offset + 1];

        record.MarkProperty(nameof(DataRecord.StatusBitsB), record.Offset + sizeof(byte), sizeof(byte));
    }

    /// <summary>
    /// Loads a forwarding record.
    /// </summary>
    /// <remarks>
    /// Forwarding stubs are used when a record is moved to a new page in a heap.
    /// F
    /// An example would be two VARCHAR(8000) columns in one table. A record could start where the total size of column 1 + column 2 is 
    /// less than the size of the page, but a subsequent update could push it over the page size. The record would then be moved to a new 
    /// page with a forwarding stub left in its place.
    /// 
    /// Record stubs are RID/Row Identifier structures that point to the new location of the record via File Id:Page Id:Slot Id.
    /// </remarks>
    private void LoadForwardingStub(DataRecord dataRecord, byte[] data)
    {
        var forwardingRecord = new byte[8];

        Array.Copy(data, dataRecord.Offset + sizeof(byte), forwardingRecord, 0, 6);

        dataRecord.ForwardingStub = new RowIdentifier(forwardingRecord);

        dataRecord.MarkProperty(nameof(DataRecord.ForwardingStub), dataRecord.Offset + sizeof(byte), 6);
    }
}