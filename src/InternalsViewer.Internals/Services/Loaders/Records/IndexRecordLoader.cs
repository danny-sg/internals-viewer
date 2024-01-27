using System.Collections;
using System.Diagnostics;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Metadata.Helpers;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Services.Loaders.Records;

/// <summary>
/// Loads an Index Record using a combination of the table structure and the record structure
/// </summary>
/// <remarks>
/// Microsoft SQL Server 2012 Internals by Kalen Delaney et al. has a good description of the index record structure in Chapter 7.
/// 
/// There are several different types of index records this loader has to parse:
/// 
/// - Clustered Index
///     - Node records
///         Note - not Leaf records - these are the data pages themselves
///     - Unique / Non-Unique (Uniqueifier)
///     
/// - Non-Clustered Index
///     - Based on a Heap or based on a Clustered Index
///     - Node records
///     - Leaf records
///     - Unique / Non-Unique
///     - Includes columns
///     
/// This is in addition to the variable/fixed length record fields.
/// </remarks>
public class IndexRecordLoader(ILogger<IndexRecordLoader> logger) : RecordLoader
{
    public ILogger<IndexRecordLoader> Logger { get; } = logger;

    /// <summary>
    /// Load an Index record at the specified offset
    /// </summary>
    public IndexRecord Load(IndexPage page, ushort offset, IndexStructure structure)
    {
        Logger.BeginScope("Index Record Loader: {FileId}:{PageId}:{Offset}", page.PageAddress.FileId, page.PageAddress.PageId, offset);

        Logger.LogDebug(structure.ToDetailString());

        var nodeType = page.PageHeader.Level > 0 ? NodeType.Node : NodeType.Leaf;

        var record = new IndexRecord
        {
            SlotOffset = offset,
            NodeType = nodeType
        };

        Logger.LogDebug("Loading Index Record ({nodeType}) at offset {offset}", nodeType, offset);

        // Indexes should always have a Status Bits A
        LoadStatusBitsA(record, page.Data);

        Logger.LogTrace("Status Bits A: {StatusBitsA}", record.StatusBitsADescription);

        // Load the null bitmap if necessary
        if (record.HasNullBitmap)
        {
            Logger.LogTrace("Has Null Bitmap flag set, loading null bitmap");

            LoadNullBitmap(record, page, structure);
        }

        // Load the variable length column offset array if necessary
        if (record.HasVariableLengthColumns)
        {
            Logger.LogTrace("Has Variable Length Columns flag set, loading offset array");

            var startIndex = record.HasNullBitmap ? 2 + record.NullBitmapSize : 0;

            LoadColumnOffsetArray(record, startIndex, page);

            // Calculate the offset of the variable length data
            record.VariableLengthDataOffset = (ushort)(page.PageHeader.FixedLengthSize
                                                       + sizeof(short)
                                                       + startIndex
                                                       + sizeof(short) * record.VariableLengthColumnCount);
        }

        Logger.LogDebug("Node Type: {NodeType}, Index Type: {IndexType}, Underlying Index Type: {ParentIndexType}",
                        record.NodeType,
                        page.AllocationUnit.IndexType,
                        structure.TableStructure?.IndexType ?? structure.IndexType);

        if (nodeType == NodeType.Node)
        {
            Logger.LogDebug("Loading Node Record");

            // A node will have a down page pointer to the next level in the b-tree
            LoadDownPagePointer(record, page);

            if (structure.IndexType == IndexType.Clustered)
            {
                LoadClusteredNode(record, page, structure);
            }
            else
            {
                LoadNonClusteredNode(record, page, structure);
            }
        }
        else
        {
            Logger.LogDebug("Loading Leaf Record");

            Debug.Assert(structure.IndexType == IndexType.NonClustered, "Leaf level on Index type pages should always be non-clustered");

            LoadNonClusteredLeaf(record, page, structure);
        }

        return record;
    }

    /// <summary>
    /// Load a clustered index node record
    /// </summary>
    /// <remarks>
    /// A clustered index node will contain the clustered key columns and a down page pointer
    /// </remarks>
    private void LoadClusteredNode(IndexRecord record, PageData page, IndexStructure structure)
    {
        var columns = structure.Columns.Where(c => c.IsKey || c.IsUniqueifier).ToList();

        LoadColumnValues(record, page, columns, NodeType.Node);
    }

    private void LoadNonClusteredNode(IndexRecord record, PageData page, IndexStructure structure)
    {
        List<IndexColumnStructure> columns;

        if (structure.TableStructure?.IndexType == IndexType.Clustered)
        {
            columns = structure.Columns.Where(c => c.IsKey || c.IsUniqueifier).ToList();
        }
        else
        {
            columns = structure.Columns;
        }

        LoadColumnValues(record, page, columns, NodeType.Node);
    }

    private void LoadNonClusteredLeaf(IndexRecord record, PageData page, IndexStructure structure)
    {
        var columns = structure.Columns;

        LoadColumnValues(record, page, columns, NodeType.Leaf);
    }

    /// <summary>
    /// Load a down page pointer (page address) pointing to the next level in the b-tree
    /// </summary>
    private static void LoadDownPagePointer(IndexRecord record, PageData page)
    {
        //Last 6 bytes of the fixed slot
        var address = new byte[PageAddress.Size];

        var downPagePointerOffset = record.SlotOffset + page.PageHeader.FixedLengthSize - PageAddress.Size;

        Array.Copy(page.Data, downPagePointerOffset, address, 0, PageAddress.Size);

        record.DownPagePointer = PageAddressParser.Parse(address);

        record.MarkProperty("DownPagePointer", downPagePointerOffset, PageAddress.Size);
    }

    /// <summary>
    /// Check if a column is a Row Identifier by looking for a specific data type/length and offset
    /// </summary>
    private static bool IsRowIdentifier(ColumnStructure indexColumn, NodeType nodeType, int fixedLengthSize)
    {
        var isBinaryDataType = indexColumn.DataType == System.Data.SqlDbType.Binary;
        var hasCorrectDataLength = indexColumn.DataLength == RowIdentifier.Size;
        var hasCorrectLeafOffset = nodeType == NodeType.Leaf && indexColumn.LeafOffset == fixedLengthSize - 8;
        var hasCorrectNodeOffset = nodeType == NodeType.Node && indexColumn.NodeOffset == fixedLengthSize - 14;

        return isBinaryDataType && hasCorrectDataLength && (hasCorrectLeafOffset || hasCorrectNodeOffset);
    }

    private void LoadColumnOffsetArray(Record record, int varColStartIndex, Page page)
    {
        var variableColumnCountOffset = record.SlotOffset + page.PageHeader.FixedLengthSize + varColStartIndex;

        record.VariableLengthColumnCount = BitConverter.ToUInt16(page.Data, variableColumnCountOffset);

        record.MarkProperty("VariableLengthColumnCount", variableColumnCountOffset, sizeof(short));

        // Load offset array of 2-byte integers indicating the end offset of each variable length field
        record.ColOffsetArray = GetOffsetArray(page.Data,
                                               record.VariableLengthColumnCount,
                                               record.SlotOffset + page.PageHeader.FixedLengthSize + sizeof(short) + varColStartIndex);

        record.MarkProperty("ColOffsetArrayDescription",
                                 variableColumnCountOffset + sizeof(short), record.VariableLengthColumnCount * sizeof(short));
    }

    private void LoadColumnValues(IndexRecord record, PageData page, List<IndexColumnStructure> columns, NodeType nodeType)
    {
        var columnValues = new List<RecordField>();

        var index = 0;

        foreach (var column in columns)
        {
            var columnOffset = nodeType == NodeType.Leaf ? column.LeafOffset : column.NodeOffset;

            var field = new RecordField(column);

            if (columnOffset >= 0)
            {
                if (IsRowIdentifier(column, nodeType, page.PageHeader.FixedLengthSize))
                {
                    LoadRidField(columnOffset, record, page.Data);
                }
                else
                {
                    // Fixed length field
                    field = LoadFixedLengthField(columnOffset, column, record, page.Data);
                }
            }
            else if (column.IsUniqueifier)
            {
                field = LoadUniqueifier(columnOffset, column, record, page.Data);
            }
            else if (record.HasVariableLengthColumns)
            {
                // Variable length field
                field = LoadVariableLengthField(columnOffset, column, record, page.Data);
            }

            record.MarkProperty("FieldsArray", field.Name, index);

            index++;

            columnValues.Add(field);
        }

        record.Fields.AddRange(columnValues);
    }

    private static void LoadRidField(int offset, IndexRecord record, byte[] pageData)
    {
        var ridAddress = new byte[8];

        Array.Copy(pageData, record.SlotOffset + offset, ridAddress, 0, RowIdentifier.Size);

        record.Rid = new RowIdentifier(ridAddress);

        record.MarkProperty("Rid", record.SlotOffset + offset, RowIdentifier.Size);
    }

    private RecordField LoadVariableLengthField(short columnOffset, ColumnStructure column, Record record, byte[] pageData)
    {
        int length;

        var field = new RecordField(column);

        var variableIndex = Math.Abs(columnOffset) - 1;

        var offset = GetVariableLengthOffset(record, variableIndex);

        if (variableIndex >= record.ColOffsetArray.Length)
        {
            length = 0;
        }
        else
        {
            length = record.ColOffsetArray[variableIndex] - offset;
        }

        var data = new byte[length];

        Array.Copy(pageData, offset + record.SlotOffset, data, 0, length);

        field.Offset = offset;
        field.Length = length;
        field.Data = data;
        field.VariableOffset = variableIndex;

        field.MarkProperty("Value", record.SlotOffset + field.Offset, field.Length);

        return field;
    }

    private static ushort GetVariableLengthOffset(Record record, int variableIndex)
    {
        ushort offset;

        if (variableIndex == 0)
        {
            // If position 0 the start of the data will be at the variable length data offset...
            offset = record.VariableLengthDataOffset;
        }
        else
        {
            // ...else use the end offset of the previous column as the start of this one
            offset = record.ColOffsetArray[variableIndex - 1];
        }

        return offset;
    }

    private RecordField LoadUniqueifier(short columnOffset, IndexColumnStructure column, IndexRecord record, byte[] pageData)
    {
        var field = new RecordField(column);

        var uniqueifierIndex = Math.Abs(columnOffset) - 1;

        //

        if (uniqueifierIndex >= record.VariableLengthColumnCount)
        {
            // If there is no slot for the uniqueifier it can be taken as zero
            return field;
        }

        var offset = GetVariableLengthOffset(record, uniqueifierIndex);

        // Uniqueifier is always a 4-byte integer
        var length = sizeof(int);

        var data = new byte[length];

        Array.Copy(pageData, offset + record.SlotOffset, data, 0, length);

        field.Offset = offset;
        field.Length = length;
        field.Data = data;

        //TODO: change to uniqueifier
        field.MarkProperty("Value", record.SlotOffset + field.Offset, field.Length);
        //record.MarkDataStructure("Uniqueifier", record.SlotOffset + field.Offset, field.Length);

        return field;
    }

    /// <summary>
    /// Loads Fixed Length Fields into a new Record Field
    /// </summary>
    /// <remarks>
    /// Fixed length fields are based on the length of the field defined in the table structure.
    /// </remarks>
    private static RecordField LoadFixedLengthField(short offset, ColumnStructure column, Record dataRecord, byte[] pageData)
    {
        var field = new RecordField(column);

        // Length fixed from data type/data length
        var length = column.DataLength;

        var data = new byte[length];

        Array.Copy(pageData, column.LeafOffset + dataRecord.SlotOffset, data, 0, length);

        field.Offset = offset;
        field.Length = length;
        field.Data = data;

        field.MarkProperty("Value", dataRecord.SlotOffset + field.Offset, field.Length);

        return field;
    }

    private void LoadNullBitmap(Record record, PageData page, IndexStructure structure)
    {
        record.NullBitmapSize = (short)((structure.Columns.Count - 1) / 8 + 1);

        var columnCountPosition = record.SlotOffset + page.PageHeader.FixedLengthSize;

        record.ColumnCount = BitConverter.ToInt16(page.Data, columnCountPosition);

        record.MarkProperty("ColumnCount", columnCountPosition, sizeof(short));

        var nullBitmapBytes = new byte[record.NullBitmapSize];

        var nullBitmapPosition = record.SlotOffset + page.PageHeader.FixedLengthSize + sizeof(short);

        Array.Copy(page.Data,
                   nullBitmapPosition,
                   nullBitmapBytes,
                   0,
                   record.NullBitmapSize);

        record.NullBitmap = new BitArray(nullBitmapBytes);

        record.MarkProperty("NullBitmapDescription", nullBitmapPosition, record.NullBitmapSize);
    }
}