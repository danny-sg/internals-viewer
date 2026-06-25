using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.FixedVarRecordType;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

/// <summary>
/// Loads an Index Record using a combination of the table structure and the record structure
/// </summary>
/// <remarks>
/// Microsoft SQL Server 2012 Internals by Kalen Delaney et al. has a good description of the index record structure in
/// Chapter 7.
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
///         ┌───────────────┬────────┬───────────┬──────────┐
///         │     Type      │ Unique │ Node Type │ Includes │
///         ├───────────────┼────────┼───────────┼──────────┤
///         │ Clustered     │ Yes    │ Root      │ No       │
///         │ Clustered     │ Yes    │ Node      │ No       │
///         │ Clustered     │ Yes    │ Leaf      │ No       │
///         │ Clustered     │ No     │ Root      │ No       │
///         │ Clustered     │ No     │ Node      │ No       │
///         │ Clustered     │ No     │ Leaf      │ No       │
///         │ Clustered     │ Yes    │ Root      │ Yes      │
///         │ Clustered     │ Yes    │ Node      │ Yes      │
///         │ Clustered     │ Yes    │ Leaf      │ Yes      │
///         │ Clustered     │ No     │ Root      │ Yes      │
///         │ Clustered     │ No     │ Node      │ Yes      │
///         │ Clustered     │ No     │ Leaf      │ Yes      │
///         │ Non-Clustered │ Yes    │ Root      │ No       │
///         │ Non-Clustered │ Yes    │ Node      │ No       │
///         │ Non-Clustered │ Yes    │ Leaf      │ No       │
///         │ Non-Clustered │ No     │ Root      │ No       │
///         │ Non-Clustered │ No     │ Node      │ No       │
///         │ Non-Clustered │ No     │ Leaf      │ No       │
///         │ Non-Clustered │ Yes    │ Root      │ Yes      │
///         │ Non-Clustered │ Yes    │ Node      │ Yes      │
///         │ Non-Clustered │ Yes    │ Leaf      │ Yes      │
///         │ Non-Clustered │ No     │ Root      │ Yes      │
///         │ Non-Clustered │ No     │ Node      │ Yes      │
///         │ Non-Clustered │ No     │ Leaf      │ Yes      │
///         └───────────────┴────────┴───────────┴──────────┘
///     
/// This is in addition to the variable/fixed length record fields.
/// </remarks>
public sealed class FixedVarIndexRecordLoader(ILogger<FixedVarIndexRecordLoader> logger) : FixedVarRecordLoader
{
    private ILogger<FixedVarIndexRecordLoader> Logger { get; } = logger;

    /// <summary>
    /// Load an Index record at the specified offset
    /// </summary>
    public FixedVarIndexRecord Load(IndexPage page, ushort offset, int slot, IndexStructure structure)
    {
        Logger.BeginScope("Index Record Loader: {FileId}:{PageId}:{Offset}",
                          page.PageAddress.FileId,
                          page.PageAddress.PageId,
                          offset);

        Logger.LogTrace("Index Structure: {Structure}", structure);

        var isLeaf = page.PageHeader.Level == 0;

        var isRoot = page.PageAddress == page.AllocationUnit.RootPage;

        var nodeType = (isRoot, isLeaf) switch
        {
            (_, true) => NodeType.Leaf,
            (true, _) => NodeType.Root,
            _ => NodeType.Node,
        };

        var record = new FixedVarIndexRecord
        {
            Offset = offset,
            Slot = slot,
            NodeType = nodeType
        };

        Logger.LogDebug("Loading Index Record ({nodeType}) at offset {offset}", nodeType, offset);

        // Indexes should always have a Status Bits A
        LoadStatusBitsA(record, page.Data);

        Logger.LogTrace("Status Bits A: {StatusBitsA}", record.StatusBitsA);

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
                                                       + (sizeof(short) * record.VariableLengthColumnCount));
        }

        Logger.LogDebug("Node Type: {NodeType}, Index Type: {IndexType}, Underlying Index Type: {ParentIndexType}",
                        record.NodeType,
                        page.AllocationUnit.IndexType,
                        structure.TableStructure?.IndexType ?? structure.IndexType);

        switch (nodeType)
        {
            case NodeType.Root:
                Logger.LogDebug("Loading Root Record");

                LoadDownPagePointer(record, page);

                if (structure.IndexType == IndexType.Clustered)
                {
                    LoadClusteredNode(record, page, structure, nodeType);
                }
                else
                {
                    LoadNonClusteredRoot(record, page, structure);
                }

                break;
            case NodeType.Node:
                {
                    Logger.LogDebug("Loading Node Record");

                    // A node will have a down page pointer to the next level in the b-tree
                    LoadDownPagePointer(record, page);

                    if (structure.IndexType == IndexType.Clustered)
                    {
                        LoadClusteredNode(record, page, structure, nodeType);
                    }
                    else
                    {
                        LoadNonClusteredNode(record, page, structure);
                    }

                    break;
                }

            case NodeType.Leaf:
                Logger.LogDebug("Loading Leaf Record");

                LoadNonClusteredLeaf(record, page, structure);
                break;
        }

        return record;
    }

    /// <summary>
    /// Load a clustered index node record
    /// </summary>
    /// <remarks>
    /// A clustered index node will contain the clustered key columns and a down page pointer
    /// </remarks>
    private void LoadClusteredNode(FixedVarIndexRecord record,
                                   PageData page, 
                                   IndexStructure structure, 
                                   NodeType nodeType)
    {
        var columns = structure.Columns.Where(c => c.IsKey || c.IsUniqueifier).ToList();

        LoadColumnValues(record, page, columns, nodeType);
    }

    private static void LoadNonClusteredNode(FixedVarIndexRecord record, PageData page, IndexStructure structure)
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

    private static void LoadNonClusteredRoot(FixedVarIndexRecord record, PageData page, IndexStructure structure)
    {
        List<IndexColumnStructure> columns;

        if (structure.TableStructure?.IndexType == IndexType.Clustered && !structure.IsUnique)
        {
            columns = structure.Columns.Where(c => c.IsKey || c.IsUniqueifier).ToList();
        }
        else
        {
            // Unique non-clustered indexes do not contain the clustered index keys in the root
            columns = structure.Columns.Where(c => c.IsIndexKey).ToList();
        }

        LoadColumnValues(record, page, columns, NodeType.Root);
    }

    private void LoadNonClusteredLeaf(FixedVarIndexRecord record, PageData page, IndexStructure structure)
    {
        var columns = structure.Columns;

        LoadColumnValues(record, page, columns, NodeType.Leaf);
    }

    /// <summary>
    /// Load a down page pointer (page address) pointing to the next level in the b-tree
    /// </summary>
    private static void LoadDownPagePointer(FixedVarIndexRecord record, PageData page)
    {
        // Last 6 bytes of the fixed slot
        var downPagePointerOffset = record.Offset + page.PageHeader.FixedLengthSize - PageAddress.Size;

        record.DownPagePointer = PageAddressParser.Parse(
            page.Data.AsSpan(downPagePointerOffset, PageAddress.Size));

        record.MarkProperty(nameof(FixedVarIndexRecord.DownPagePointer), downPagePointerOffset, PageAddress.Size);
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

    private static void LoadColumnOffsetArray(FixedVarIndexRecord record, int varColStartIndex, Page page)
    {
        var variableColumnCountOffset = record.Offset + page.PageHeader.FixedLengthSize + varColStartIndex;

        record.VariableLengthColumnCount = BitConverter.ToUInt16(page.Data, variableColumnCountOffset);

        record.MarkProperty(nameof(FixedVarIndexRecord.VariableLengthColumnCount), 
                            variableColumnCountOffset, 
                            sizeof(short));

        var offset =
            record.Offset + page.PageHeader.FixedLengthSize + sizeof(short) + varColStartIndex;

        // Load offset array of 2-byte integers indicating the end offset of each variable length field
        record.VariableLengthColumnOffsetArray = RecordHelpers.GetOffsetArray(page.Data,
                                                                              record.VariableLengthColumnCount,
                                                                              offset);

        record.MarkProperty(nameof(FixedVarIndexRecord.VariableLengthColumnOffsetArray),
                            variableColumnCountOffset + sizeof(short),
                            record.VariableLengthColumnCount * sizeof(short));
    }

    private static void LoadColumnValues(FixedVarIndexRecord record,
                                         PageData page,
                                         List<IndexColumnStructure> columns,
                                         NodeType nodeType)
    {
        var columnValues = new List<FixedVarRecordField>();

        foreach (var column in columns)
        {
            var columnOffset = nodeType == NodeType.Leaf ? column.LeafOffset : column.NodeOffset;

            var field = new FixedVarRecordField(column);

            if (record.HasNullBitmap && record.IsNullBitmapSet(column, 0))
            {
                // Null bitmap is set
                field = LoadNullField(column);
            }
            else if (nodeType != NodeType.Leaf
                     && record.Slot == 0
                     && page.PageHeader.PreviousPage == PageAddress.Empty)
            {
                // Sentinel - null low-key record at left of b-tree
                field = LoadNullField(column);
            }
            else if (columnOffset >= 0)
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

            columnValues.Add(field);
        }

        record.Fields.AddRange(columnValues);
    }

    private static FixedVarRecordField LoadNullField(IndexColumnStructure column)
    {
        var nullField = new FixedVarRecordField(column);

        nullField.MarkProperty(nameof(FixedVarRecordField.Value));

        return nullField;
    }

    private static void LoadRidField(int offset, FixedVarIndexRecord record, byte[] pageData)
    {
        record.Rid = new RowIdentifier(
            pageData[(record.Offset + offset)..(record.Offset + offset + RowIdentifier.Size)]);

        record.MarkProperty(nameof(FixedVarIndexRecord.Rid), record.Offset + offset, RowIdentifier.Size);
    }

    private static FixedVarRecordField LoadVariableLengthField(short columnOffset,
                                                               ColumnStructure column,
                                                               FixedVarIndexRecord record,
                                                               byte[] pageData)
    {
        int length;

        var field = new FixedVarRecordField(column);

        var variableIndex = Math.Abs(columnOffset) - 1;

        var offset = GetVariableLengthOffset(record, variableIndex);

        if (variableIndex >= record.VariableLengthColumnOffsetArray.Length)
        {
            length = 0;
        }
        else
        {
            length = record.VariableLengthColumnOffsetArray[variableIndex] - offset;
        }

        field.Offset = offset;
        field.Length = length;
        field.Data = pageData.AsMemory(offset + record.Offset, length);
        field.VariableOffset = variableIndex;

        record.MarkValue(ItemType.VariableLengthValue,
                         column.ColumnName,
                         field,
                         record.Offset + field.Offset,
                         field.Length);

        return field;
    }

    private static ushort GetVariableLengthOffset(FixedVarIndexRecord record, int variableIndex)
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
            offset = record.VariableLengthColumnOffsetArray[variableIndex - 1];
        }

        return offset;
    }

    private static FixedVarRecordField LoadUniqueifier(short columnOffset,
                                                      IndexColumnStructure column,
                                                      FixedVarIndexRecord record,
                                                      byte[] pageData)
    {
        var field = new FixedVarRecordField(column);

        var uniqueifierIndex = Math.Abs(columnOffset) - 1;

        if (uniqueifierIndex >= record.VariableLengthColumnCount)
        {
            // If there is no slot for the uniqueifier it can be taken as zero
            return field;
        }

        var offset = GetVariableLengthOffset(record, uniqueifierIndex);

        // Uniqueifier is always a 4-byte integer
        var length = sizeof(int);

        field.Offset = offset;
        field.Length = length;
        field.Data = pageData.AsMemory(offset + record.Offset, length);

        record.MarkValue(ItemType.UniquifierIndex, "Uniquifier", field, record.Offset + field.Offset, field.Length);

        return field;
    }

    /// <summary>
    /// Loads Fixed Length Fields into a new Record Field
    /// </summary>
    /// <remarks>
    /// Fixed length fields are based on the length of the field defined in the table structure.
    /// </remarks>
    private static FixedVarRecordField LoadFixedLengthField(short offset,
                                                            ColumnStructure column,
                                                            Record record,
                                                            byte[] pageData)
    {
        var field = new FixedVarRecordField(column);

        // Length fixed from data type/data length
        var length = column.DataLength;

        field.Offset = offset;
        field.Length = length;
        field.Data = pageData.AsMemory(record.Offset + field.Offset, length);

        record.MarkValue(ItemType.FixedLengthValue,
                         column.ColumnName,
                         field,
                         record.Offset + field.Offset,
                         field.Length);

        return field;
    }

    private static void LoadNullBitmap(FixedVarIndexRecord record, PageData page, IndexStructure structure)
    {
        record.NullBitmapSize = (short)(((structure.Columns.Count - 1) / 8) + 1);

        var columnCountPosition = record.Offset + page.PageHeader.FixedLengthSize;

        record.ColumnCount = BitConverter.ToInt16(page.Data, columnCountPosition);

        record.MarkProperty(nameof(FixedVarIndexRecord.ColumnCount), columnCountPosition, sizeof(short));

        var nullBitmapPosition = record.Offset + page.PageHeader.FixedLengthSize + sizeof(short);

        record.NullBitmap = page.Data[nullBitmapPosition..(nullBitmapPosition + record.NullBitmapSize)];

        record.MarkProperty(nameof(FixedVarIndexRecord.NullBitmap), nullBitmapPosition, record.NullBitmapSize);
    }
}