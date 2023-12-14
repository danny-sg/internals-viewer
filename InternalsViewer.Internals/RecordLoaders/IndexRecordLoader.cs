using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Services.Loaders.Records;

namespace InternalsViewer.Internals.RecordLoaders;

internal class IndexRecordLoader : RecordLoader
{
    internal static void Load(IndexRecord record)
    {
        var varColStartIndex = 0;

        LoadIndexType(record);

        LoadStatusBits(record);

        if (record.HasNullBitmap)
        {
            LoadNullBitmap(record);

            varColStartIndex = 2 + record.NullBitmapSize;
        }

        if (record.HasVariableLengthColumns)
        {
            LoadColumnOffsetArray(record, varColStartIndex);
        }

        record.VariableLengthDataOffset = (ushort)(record.Page.Header.MinLen + sizeof(short) + varColStartIndex + (sizeof(short) * record.VariableLengthColumnCount));

        LoadColumnValues(record);

        if (record.IsIndexType(IndexTypes.Node) | record.Page.Header.IndexId == 1)
        {
            LoadDownPagePointer(record);
        }

        if (record.IsIndexType(IndexTypes.Heap) && (!((IndexStructure)record.Structure).IsUnique | record.IsIndexType(IndexTypes.Leaf)))
        {
            LoadRid(record);
        }
    }

    private static void LoadDownPagePointer(IndexRecord record)
    {
        //Last 6 bytes of the fixed slot
        var address = new byte[PageAddress.Size];

        var downPagePointerOffset = record.SlotOffset + record.Page.Header.MinLen - PageAddress.Size;

        Array.Copy(record.Page.PageData, downPagePointerOffset, address, 0, PageAddress.Size);

        record.DownPagePointer = PageAddressParser.Parse(address);

        record.MarkDataStructure("DownPagePointer", downPagePointerOffset, PageAddress.Size);
    }

    private static void LoadRid(IndexRecord record)
    {
        int ridOffset;
        var ridAddress = new byte[8];

        if (record.IsIndexType(IndexTypes.Leaf))
        {
            ridOffset = record.SlotOffset + record.Page.Header.MinLen - 8;
        }
        else
        {
            ridOffset = record.SlotOffset + record.Page.Header.MinLen - 14;
        }

        Array.Copy(record.Page.PageData, ridOffset, ridAddress, 0, RowIdentifier.Size);

        record.Rid = new RowIdentifier(ridAddress);

        record.MarkDataStructure("Rid", ridOffset, RowIdentifier.Size);
    }

    private static void LoadColumnOffsetArray(IndexRecord record, int varColStartIndex)
    {
        var varColCountOffset = record.SlotOffset + record.Page.Header.MinLen + varColStartIndex;

        record.VariableLengthColumnCount = BitConverter.ToUInt16(record.Page.PageData, varColCountOffset);

        record.MarkDataStructure("VariableLengthColumnCount", varColCountOffset, sizeof(short));

        // Load offset array of 2-byte ints indicating the end offset of each variable length field
        record.ColOffsetArray = GetOffsetArray(record.Page.PageData,
            record.VariableLengthColumnCount,
            record.SlotOffset + record.Page.Header.MinLen + sizeof(short) + varColStartIndex);

        record.MarkDataStructure("ColOffsetArrayDescription", varColCountOffset + sizeof(short), record.VariableLengthColumnCount * sizeof(short));
    }

    private static void LoadColumnValues(IndexRecord record)
    {
        var columnValues = new List<RecordField>();

        var index = 0;

        foreach (var column in (record.Structure as IndexStructure).Columns)
        {
            var indexCol = (IndexColumn)column;
            var processKeyColumn = !indexCol.Key || (record.IncludeKey && indexCol.Key);
            var processIncludesColumn = !indexCol.IncludedColumn || (indexCol.IncludedColumn && record.IsIndexType(IndexTypes.Leaf));

            if (processKeyColumn & processIncludesColumn)
            {
                var field = new RecordField(indexCol);

                var length = 0;
                var offset = 0;
                byte[] data = null;
                var variableIndex = 0;

                if (indexCol.LeafOffset >= 0)
                {
                    // Fixed length field
                    offset = indexCol.LeafOffset;
                    length = indexCol.DataLength;
                    data = new byte[length];

                    Array.Copy(record.Page.PageData, indexCol.LeafOffset + record.SlotOffset, data, 0, length);
                }
                else if (record.HasVariableLengthColumns)
                {
                    //Variable length field
                    variableIndex = (indexCol.LeafOffset * -1) - 1;

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

                    Array.Copy(record.Page.PageData, offset + record.SlotOffset, data, 0, length);
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

    private static void LoadNullBitmap(IndexRecord record)
    {
        record.NullBitmapSize = (short)((record.Structure.Columns.Count - 1) / 8 + 1);

        var columnCountPosition = record.SlotOffset + record.Page.Header.MinLen;

        record.ColumnCount = BitConverter.ToInt16(record.Page.PageData, columnCountPosition);

        record.MarkDataStructure("ColumnCount", columnCountPosition, sizeof(short));

        var nullBitmapBytes = new byte[record.NullBitmapSize];

        var nullBitmapPosition = record.SlotOffset + record.Page.Header.MinLen + sizeof(short);

        Array.Copy(record.Page.PageData,
            nullBitmapPosition,
            nullBitmapBytes,
            0,
            record.NullBitmapSize);

        record.NullBitmap = new BitArray(nullBitmapBytes);

        record.MarkDataStructure("NullBitmapDescription", nullBitmapPosition, record.NullBitmapSize);
    }

    private static void LoadStatusBits(IndexRecord record)
    {
        var statusA = record.Page.PageData[record.SlotOffset];

        record.StatusBitsA = new BitArray(new[] { statusA });

        record.MarkDataStructure("StatusBitsADescription", record.SlotOffset, 1);

        record.RecordType = (RecordType)((statusA >> 1) & 7);

        record.HasNullBitmap = record.StatusBitsA[4];
        record.HasVariableLengthColumns = record.StatusBitsA[5];
    }

    private static void LoadIndexType(IndexRecord record)
    {
        if (record.Page.Header.IndexId > 0)
        {
            record.IndexType |= IndexTypes.NonClustered;
        }
        else
        {
            record.IndexType |= IndexTypes.Clustered;
        }

        if (record.Page.Header.Level > 0)
        {
            record.IndexType |= IndexTypes.Node;
        }
        else
        {
            record.IndexType |= IndexTypes.Leaf;
        }

        if (((IndexStructure)record.Structure).IsHeap)
        {
            record.IndexType |= IndexTypes.Heap;
        }
        else
        {
            record.IndexType |= IndexTypes.TableClustered;
        }

        record.IncludeKey = (!((IndexStructure)record.Structure).IsUnique
                             && record.IsIndexType(IndexTypes.NonClustered))
                            || record.IsIndexType(IndexTypes.NonClusteredLeaf);

    }
}