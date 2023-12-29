using System.Collections;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Metadata;

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
///     - Based on a Heap/based on a Clustered Index
///     - Node records
///     - Leaf records
///     - Unique / Non-Unique
///     - Includes columns
///     
/// This is in addition to the variable/fixed length record fields.
/// </remarks>
public class IndexRecordLoader : RecordLoader
{
    /// <summary>
    /// Load an Index record at the specified offset
    /// </summary>
    /// <remarks>
    /// An Index record will have the following structure:
    /// 
    ///     Status Bits A                              - 1 byte
    /// 
    /// </remarks>
    public static IndexRecord Load(IndexPage page, ushort offset, IndexStructure structure)
    {
        var record = new IndexRecord
        {
            SlotOffset = offset,
            NodeType = page.PageHeader.Level > 0 ? NodeType.Node : NodeType.Leaf
        };

        // Indexes should always have a Status Bits A
        LoadStatusBitsA(record, page.Data);

        // Load the null bitmap if necessary
        if (record.HasNullBitmap)
        {
            LoadNullBitmap(record, page, structure);
        }

        // Load the variable length column offset array if necessary
        if (record.HasVariableLengthColumns)
        {
            var startIndex = record.HasNullBitmap ? 2 + record.NullBitmapSize : 0;

            LoadColumnOffsetArray(record, startIndex, page);

            record.VariableLengthDataOffset = (ushort)(page.PageHeader.MinLen
                                                       + sizeof(short)
                                                       + startIndex
                                                       + sizeof(short) * record.VariableLengthColumnCount);
        }

        switch (record.NodeType)
        {
            case NodeType.Node when page.AllocationUnit.IndexType == IndexType.Clustered:
                LoadClusteredNode(record, page, structure);
                break;
            case NodeType.Node when page.AllocationUnit.IndexType == IndexType.NonClustered:
                LoadNonClusteredNode(record, page);
                break;
            case NodeType.Leaf when page.AllocationUnit.IndexType == IndexType.NonClustered:
                LoadNonClusteredLeaf(record, page);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unrecognised type - {page.AllocationUnit.ParentIndexType}");
        }


        return record;

        //if (record.HasNullBitmap)
        //{
        //    LoadNullBitmap(record, page, structure);

        //    varColStartIndex = 2 + record.NullBitmapSize;
        //}

        //if (record.HasVariableLengthColumns)
        //{
        //    LoadColumnOffsetArray(record, varColStartIndex, page);
        //}



        //LoadColumnValues(record, page, structure);

        //if (record.IsIndexType(IndexTypes.Node) | page.AllocationUnit.IndexId == 1)
        //{
        //    LoadDownPagePointer(record, page);
        //}

        //if (record.IsIndexType(IndexTypes.Heap) && !structure.IsUnique | record.IsIndexType(IndexTypes.Leaf))
        //{
        //    LoadRid(record, page);
        //}

        //return record;
    }

    /// <summary>
    /// Load a clustered index node record
    /// </summary>
    /// <remarks>
    /// A clustered index node will contain the clustered key columns and a down page pointer
    /// </remarks>
    private static void LoadClusteredNode(IndexRecord record, PageData page, IndexStructure structure)
    {
        var columns = structure.Columns.Where(c => c.IsKey).ToList();

        LoadColumnValues(record, page, columns, NodeType.Node);    

        LoadDownPagePointer(record, page);
    }

    private static void LoadNonClusteredNode(IndexRecord record, PageData page)
    {

    }

    private static void LoadNonClusteredLeaf(IndexRecord record, PageData page)
    {

    }

    private static void LoadDownPagePointer(IndexRecord record, PageData page)
    {
        //Last 6 bytes of the fixed slot
        var address = new byte[PageAddress.Size];

        var downPagePointerOffset = record.SlotOffset + page.PageHeader.MinLen - PageAddress.Size;

        Array.Copy(page.Data, downPagePointerOffset, address, 0, PageAddress.Size);

        record.DownPagePointer = PageAddressParser.Parse(address);

        record.MarkDataStructure("DownPagePointer", downPagePointerOffset, PageAddress.Size);
    }

    private static void LoadRid(IndexRecord record, PageData page)
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

    private static void LoadColumnOffsetArray(IndexRecord record, int varColStartIndex, Page page)
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

    private static void LoadColumnValues(IndexRecord record, PageData page, List<IndexColumnStructure> columns, NodeType nodeType)
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
                offset = columnOffset;
                length = column.DataLength;
                data = new byte[length];

                Array.Copy(page.Data, offset + record.SlotOffset, data, 0, length);
            }
            else if (record.HasVariableLengthColumns)
            {
                //Variable length field
                variableIndex = column.LeafOffset * -1 - 1;

                if (variableIndex == 0)
                {
                    offset = record.VariableLengthDataOffset;
                }
                else
                {
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

    private static void LoadNullBitmap(Record record, PageData page, IndexStructure structure)
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

    private static void LoadIndexType(IndexRecord record, AllocationUnitPage page, IndexStructure structure)
    {


        //TODO: Remember what this is for
        record.IncludeKey = !structure.IsUnique;
        //
        //                     && record.IsIndexType(IndexTypes.NonClustered);
        //                    || record.IsIndexType(IndexTypes.NonClusteredLeaf);
    }
}