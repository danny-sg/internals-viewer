﻿using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    /// <remarks>
    /// An Index record will have the following structure:
    /// 
    ///     Status Bits A                              - 1 byte
    /// 
    /// </remarks>
    public IndexRecord Load(IndexPage page, ushort offset, IndexStructure structure)
    {
        Logger.BeginScope("Index Record Loader: {Page}:{Offset}", page, offset);

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
            record.VariableLengthDataOffset = (ushort)(page.PageHeader.MinLen
                                                       + sizeof(short)
                                                       + startIndex
                                                       + sizeof(short) * record.VariableLengthColumnCount);
        }

        Logger.LogDebug("Node Type: {NodeType}, Index Type: {IndexType}, Underlying Index Type: {ParentIndexType}",
                        record.NodeType,
                        page.AllocationUnit.IndexType,
                        structure.BaseIndexStructure?.IndexType ?? structure.IndexType);

        if(nodeType == NodeType.Node)
        {
            Logger.LogDebug("Loading Node Record");

            // A node will have a down page pointer to the next level in the b-tree
            LoadDownPagePointer(record, page);

            if(structure.IndexType == IndexType.Clustered)
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

        //if (record.IsIndexType(IndexTypes.Heap) && !structure.IsUnique | record.IsIndexType(IndexTypes.Leaf))
        //{
        //    LoadRid(record, page);
        //}
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
        var columns = structure.Columns.Where(c => c.IsKey || c.IsUniqueifier).ToList();

        if (structure.BaseIndexStructure?.IndexType == IndexType.Clustered)
        {
            // Add the underlying clustered index key columns (not already included) as a non-clustered index based on a clustered index
            // will have the clustered key columns
            columns.AddRange(structure.BaseIndexStructure
                                      .Columns
                                      .Where(c => (c.IsKey || c.IsUniqueifier) && columns.All(e => e.ColumnName != c.ColumnName)));
        }
        else
        {
            // A non-clustered index based on a heap will have a RID to point to the record
            LoadRid(record, page);
        }

        LoadColumnValues(record, page, columns, NodeType.Node);
    }

    private void LoadNonClusteredLeaf(IndexRecord record, PageData page, IndexStructure structure)
    {
        var columns = structure.Columns.Where(c => c.IsKey || !structure.IsUnique || c.IsIncludeColumn).ToList();

        LoadColumnValues(record, page, columns, NodeType.Leaf);

        // A heap has a RID to point to the record. The row for a clustered table will be accessible via the key values at the leaf level
        if (structure.BaseIndexStructure?.IndexType == IndexType.Heap)
        {
            LoadRid(record, page);
        }
    }

    /// <summary>
    /// Load a down page pointer (page address) pointing to the next level in the b-tree
    /// </summary>
    private static void LoadDownPagePointer(IndexRecord record, PageData page)
    {
        //Last 6 bytes of the fixed slot
        var address = new byte[PageAddress.Size];

        var downPagePointerOffset = record.SlotOffset + page.PageHeader.MinLen - PageAddress.Size;

        Array.Copy(page.Data, downPagePointerOffset, address, 0, PageAddress.Size);

        record.DownPagePointer = PageAddressParser.Parse(address);

        record.MarkDataStructure("DownPagePointer", downPagePointerOffset, PageAddress.Size);
    }

    /// <summary>
    /// Load a RID (Row Identifier) for a heap record pointing to a specific page and slot
    /// </summary>
    private void LoadRid(IndexRecord record, PageData page)
    {
        int ridOffset;
        var ridAddress = new byte[8];

        if (record.NodeType == NodeType.Leaf)
        {
            ridOffset = record.SlotOffset + page.PageHeader.MinLen - 8;
        }
        else
        {
            ridOffset = record.SlotOffset + page.PageHeader.MinLen - 14;
        }

        Array.Copy(page.Data, ridOffset, ridAddress, 0, RowIdentifier.Size);

        record.Rid = new RowIdentifier(ridAddress);

        record.MarkDataStructure("Rid", ridOffset, RowIdentifier.Size);
    }

    private void LoadColumnOffsetArray(IndexRecord record, int varColStartIndex, Page page)
    {
        var variableColumnCountOffset = record.SlotOffset + page.PageHeader.MinLen + varColStartIndex;

        record.VariableLengthColumnCount = BitConverter.ToUInt16(page.Data, variableColumnCountOffset);

        record.MarkDataStructure("VariableLengthColumnCount", variableColumnCountOffset, sizeof(short));

        // Load offset array of 2-byte integers indicating the end offset of each variable length field
        record.ColOffsetArray = GetOffsetArray(page.Data,
                                               record.VariableLengthColumnCount,
                                               record.SlotOffset + page.PageHeader.MinLen + sizeof(short) + varColStartIndex);

        record.MarkDataStructure("ColOffsetArrayDescription",
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

            var length = 0;
            var offset = 0;
            var data = Array.Empty<byte>();

            var variableIndex = 0;

            if (columnOffset >= 0)
            {
                // Fixed length field

                data = new byte[column.DataLength];

                Array.Copy(page.Data, columnOffset + record.SlotOffset, data, 0, column.DataLength);

                offset = columnOffset;
                length = column.DataLength;
            }
            else if (column.IsUniqueifier)
            {
                var uniqueifierIndex = Math.Abs(columnOffset) - 1;

                if (uniqueifierIndex >= record.VariableLengthColumnCount)
                {
                    // If there is no slot for the uniqueifier it can be taken as zero
                    continue;
                }

                offset = record.ColOffsetArray[uniqueifierIndex - 1];

                // Uniqueifier is always a 4-byte integer
                length = sizeof(int);

                data = new byte[length];

                Array.Copy(page.Data, offset + record.SlotOffset, data, 0, length);
            }
            else if (record.HasVariableLengthColumns)
            {
                // Variable length field

                variableIndex = Math.Abs(columnOffset) - 1;

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

                if (variableIndex >= record.ColOffsetArray.Length)
                {
                    length = 0;
                }
                else
                {
                    length = record.ColOffsetArray[variableIndex] - offset;
                }

                data = new byte[length];

                Array.Copy(page.Data, offset + record.SlotOffset, data, 0, length);
            }

            field.Offset = offset;
            field.Length = length;
            field.Data = data;
            field.VariableOffset = variableIndex;

            field.MarkDataStructure("Value", record.SlotOffset + field.Offset, field.Length);

            record.MarkDataStructure("FieldsArray", field.Name, index);

            index++;

            columnValues.Add(field);
        }

        record.Fields.AddRange(columnValues);
    }

    private void LoadNullBitmap(Record record, PageData page, IndexStructure structure)
    {
        record.NullBitmapSize = (short)((structure.Columns.Count - 1) / 8 + 1);

        var columnCountPosition = record.SlotOffset + page.PageHeader.MinLen;

        record.ColumnCount = BitConverter.ToInt16(page.Data, columnCountPosition);

        record.MarkDataStructure("ColumnCount", columnCountPosition, sizeof(short));

        var nullBitmapBytes = new byte[record.NullBitmapSize];

        var nullBitmapPosition = record.SlotOffset + page.PageHeader.MinLen + sizeof(short);

        Array.Copy(page.Data,
                   nullBitmapPosition,
                   nullBitmapBytes,
                   0,
                   record.NullBitmapSize);

        record.NullBitmap = new BitArray(nullBitmapBytes);

        record.MarkDataStructure("NullBitmapDescription", nullBitmapPosition, record.NullBitmapSize);
    }
}