using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Services.Records.Loaders;

internal class IndexRecordLoader : RecordLoader
{
    internal static IndexRecord Load(Page page, ushort offset, IndexStructure structure)
    {
        var record = new IndexRecord
        {
            SlotOffset = offset
        };

        var varColStartIndex = 0;

        LoadIndexType(record, page, structure);

        LoadStatusBits(record, page);

        if (record.HasNullBitmap)
        {
            LoadNullBitmap(record, page, structure);

            varColStartIndex = 2 + record.NullBitmapSize;
        }

        if (record.HasVariableLengthColumns)
        {
            LoadColumnOffsetArray(record, varColStartIndex, page);
        }

        record.VariableLengthDataOffset = (ushort)(page.PageHeader.MinLen
                                                   + sizeof(short)
                                                   + varColStartIndex
                                                   + sizeof(short) * record.VariableLengthColumnCount);

        LoadColumnValues(record, page, structure);

        if (record.IsIndexType(IndexTypes.Node) | page.PageHeader.IndexId == 1)
        {
            LoadDownPagePointer(record, page);
        }

        if (record.IsIndexType(IndexTypes.Heap) && !structure.IsUnique | record.IsIndexType(IndexTypes.Leaf))
        {
            LoadRid(record, page);
        }

        return record;
    }

    private static void LoadDownPagePointer(IndexRecord record, Page page)
    {
        //Last 6 bytes of the fixed slot
        var address = new byte[PageAddress.Size];

        var downPagePointerOffset = record.SlotOffset + page.PageHeader.MinLen - PageAddress.Size;

        Array.Copy(page.PageData, downPagePointerOffset, address, 0, PageAddress.Size);

        record.DownPagePointer = PageAddressParser.Parse(address);

        record.MarkDataStructure("DownPagePointer", downPagePointerOffset, PageAddress.Size);
    }

    private static void LoadRid(IndexRecord record, Page page)
    {
        int ridOffset;
        var ridAddress = new byte[8];

        if (record.IsIndexType(IndexTypes.Leaf))
        {
            ridOffset = record.SlotOffset + page.PageHeader.MinLen - 8;
        }
        else
        {
            ridOffset = record.SlotOffset + page.PageHeader.MinLen - 14;
        }

        Array.Copy(page.PageData, ridOffset, ridAddress, 0, RowIdentifier.Size);

        record.Rid = new RowIdentifier(ridAddress);

        record.MarkDataStructure("Rid", ridOffset, RowIdentifier.Size);
    }

    private static void LoadColumnOffsetArray(IndexRecord record, int varColStartIndex, Page page)
    {
        var varColCountOffset = record.SlotOffset + page.PageHeader.MinLen + varColStartIndex;

        record.VariableLengthColumnCount = BitConverter.ToUInt16(page.PageData, varColCountOffset);

        record.MarkDataStructure("VariableLengthColumnCount", varColCountOffset, sizeof(short));

        // Load offset array of 2-byte ints indicating the end offset of each variable length field
        record.ColOffsetArray = GetOffsetArray(page.PageData,
                                               record.VariableLengthColumnCount,
                                               record.SlotOffset + page.PageHeader.MinLen + sizeof(short) + varColStartIndex);

        record.MarkDataStructure("ColOffsetArrayDescription",
                                 varColCountOffset + sizeof(short), record.VariableLengthColumnCount * sizeof(short));
    }

    private static void LoadColumnValues(IndexRecord record, Page page, IndexStructure structure)
    {
        var columnValues = new List<RecordField>();

        var index = 0;

        foreach (var column in structure.Columns)
        {
            var indexCol = (IndexColumnStructure)column;
            var processKeyColumn = !indexCol.Key || record.IncludeKey && indexCol.Key;
            var processIncludesColumn = !indexCol.IncludedColumn || indexCol.IncludedColumn && record.IsIndexType(IndexTypes.Leaf);

            if (processKeyColumn & processIncludesColumn)
            {
                var field = new RecordField(indexCol);

                var length = 0;
                var offset = 0;
                byte[] data = Array.Empty<byte>();
                var variableIndex = 0;

                if (indexCol.LeafOffset >= 0)
                {
                    // Fixed length field
                    offset = indexCol.LeafOffset;
                    length = indexCol.DataLength;
                    data = new byte[length];

                    Array.Copy(page.PageData, indexCol.LeafOffset + record.SlotOffset, data, 0, length);
                }
                else if (record.HasVariableLengthColumns)
                {
                    //Variable length field
                    variableIndex = indexCol.LeafOffset * -1 - 1;

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

                    Array.Copy(page.PageData, offset + record.SlotOffset, data, 0, length);
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
        }

        record.Fields.AddRange(columnValues);
    }

    private static void LoadNullBitmap(IndexRecord record, Page page, IndexStructure structure)
    {
        record.NullBitmapSize = (short)((structure.Columns.Count - 1) / 8 + 1);

        var columnCountPosition = record.SlotOffset + page.PageHeader.MinLen;

        record.ColumnCount = BitConverter.ToInt16(page.PageData, columnCountPosition);

        record.MarkDataStructure("ColumnCount", columnCountPosition, sizeof(short));

        var nullBitmapBytes = new byte[record.NullBitmapSize];

        var nullBitmapPosition = record.SlotOffset + page.PageHeader.MinLen + sizeof(short);

        Array.Copy(page.PageData,
            nullBitmapPosition,
            nullBitmapBytes,
            0,
            record.NullBitmapSize);

        record.NullBitmap = new BitArray(nullBitmapBytes);

        record.MarkDataStructure("NullBitmapDescription", nullBitmapPosition, record.NullBitmapSize);
    }

    private static void LoadStatusBits(IndexRecord record, Page page)
    {
        var statusA = page.PageData[record.SlotOffset];

        record.StatusBitsA = new BitArray(new[] { statusA });

        record.MarkDataStructure("StatusBitsADescription", record.SlotOffset, 1);

        record.RecordType = (RecordType)(statusA >> 1 & 7);

        record.HasNullBitmap = record.StatusBitsA[4];
        record.HasVariableLengthColumns = record.StatusBitsA[5];
    }

    private static void LoadIndexType(IndexRecord record, Page page, IndexStructure structure)
    {
        if (page.PageHeader.IndexId > 0)
        {
            record.IndexType |= IndexTypes.NonClustered;
        }
        else
        {
            record.IndexType |= IndexTypes.Clustered;
        }

        if (page.PageHeader.Level > 0)
        {
            record.IndexType |= IndexTypes.Node;
        }
        else
        {
            record.IndexType |= IndexTypes.Leaf;
        }

        if (structure.IsHeap)
        {
            record.IndexType |= IndexTypes.Heap;
        }
        else
        {
            record.IndexType |= IndexTypes.TableClustered;
        }

        record.IncludeKey = !structure.IsUnique
                             && record.IsIndexType(IndexTypes.NonClustered)
                            || record.IsIndexType(IndexTypes.NonClusteredLeaf);

    }
}