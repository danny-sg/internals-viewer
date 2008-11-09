using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Records;
using System.Collections;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.RecordLoaders
{
    class IndexRecordLoader : RecordLoader
    {
        internal static void Load(IndexRecord record)
        {
            int varColStartIndex = 0;

            IndexRecordLoader.LoadIndexType(record);

            IndexRecordLoader.LoadStatusBits(record);

            if (record.HasNullBitmap)
            {
                IndexRecordLoader.LoadNullBitmap(record);

                varColStartIndex = 2 + record.NullBitmapSize;
            }

            if (record.HasVariableLengthColumns)
            {
                LoadColumnOffsetArray(record, varColStartIndex);
            }

            record.VariableLengthDataOffset = (ushort)(record.Page.Header.MinLen + sizeof(Int16) + varColStartIndex + (sizeof(Int16) * record.VariableLengthColumnCount));

            IndexRecordLoader.LoadColumnValues(record);

            if (record.IsIndexType(IndexTypes.Node) | record.Page.Header.IndexId == 1)
            {
                IndexRecordLoader.LoadDownPagePointer(record);
            }

            if (record.IsIndexType(IndexTypes.Heap) && (!(record.Structure as IndexStructure).Unique | record.IsIndexType(IndexTypes.Leaf)))
            {
                IndexRecordLoader.LoadRid(record);
            }
        }

        private static void LoadDownPagePointer(IndexRecord record)
        {
            //Last 6 bytes of the fixed slot
            byte[] address = new byte[PageAddress.Size];

            int downPagePointerOffset = record.SlotOffset + record.Page.Header.MinLen - PageAddress.Size;

            Array.Copy(record.Page.PageData, downPagePointerOffset, address, 0, PageAddress.Size);

            record.DownPagePointer = new PageAddress(address);

            record.Mark("DownPagePointer", downPagePointerOffset, PageAddress.Size);
        }

        private static void LoadRid(IndexRecord record)
        {
            int ridOffset;
            byte[] ridAddress = new byte[8];

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

            record.Mark("Rid", ridOffset, RowIdentifier.Size);
        }

        private static void LoadColumnOffsetArray(IndexRecord record, int varColStartIndex)
        {
            int varColCountOffset = record.SlotOffset + record.Page.Header.MinLen + varColStartIndex;

            record.VariableLengthColumnCount = BitConverter.ToUInt16(record.Page.PageData, varColCountOffset);

            record.Mark("VariableLengthColumnCount", varColCountOffset, sizeof(Int16));

            // Load offset array of 2-byte ints indicating the end offset of each variable length field
            record.ColOffsetArray = GetOffsetArray(record.Page.PageData,
                                                       record.VariableLengthColumnCount,
                                                       record.SlotOffset + record.Page.Header.MinLen + sizeof(Int16) + varColStartIndex);

            record.Mark("ColOffsetArrayDescription", varColCountOffset + sizeof(Int16), record.VariableLengthColumnCount * sizeof(Int16));
        }

        private static void LoadColumnValues(IndexRecord record)
        {
            RecordField field;

            List<RecordField> columnValues = new List<RecordField>();

            int index = 0;

            foreach (IndexColumn indexCol in (record.Structure as IndexStructure).Columns)
            {
                bool processKeyColumn = !indexCol.Key || (record.IncludeKey && indexCol.Key);
                bool processIncludesColumn = !indexCol.IncludedColumn || (indexCol.IncludedColumn && record.IsIndexType(IndexTypes.Leaf));

                if (processKeyColumn & processIncludesColumn)
                {
                    field = new RecordField(indexCol);

                    int length = 0;
                    int offset = 0;
                    byte[] data = null;
                    int variableIndex = 0;

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

                    field.Mark("Value", record.SlotOffset + field.Offset, field.Length);


                    record.Mark("FieldsArray", field.Name, index);

                    index++;

                    columnValues.Add(field);
                }
            }

            record.Fields.AddRange(columnValues);
        }

        private static void LoadNullBitmap(IndexRecord record)
        {
            record.NullBitmapSize = (Int16)((record.Structure.Columns.Count - 1) / 8 + 1);

            int columnCountPosition = record.SlotOffset + record.Page.Header.MinLen;

            record.ColumnCount = BitConverter.ToInt16(record.Page.PageData, columnCountPosition);

            record.Mark("ColumnCount", columnCountPosition, sizeof(Int16));

            byte[] nullBitmapBytes = new byte[record.NullBitmapSize];

            int nullBitmapPosition = record.SlotOffset + record.Page.Header.MinLen + sizeof(Int16);

            Array.Copy(record.Page.PageData,
                       nullBitmapPosition,
                       nullBitmapBytes,
                       0,
                       record.NullBitmapSize);

            record.NullBitmap = new BitArray(nullBitmapBytes);

            record.Mark("NullBitmapDescription", nullBitmapPosition, record.NullBitmapSize);
        }

        private static void LoadStatusBits(IndexRecord record)
        {
            byte statusA = record.Page.PageData[record.SlotOffset];

            record.StatusBitsA = new BitArray(new byte[] { statusA });

            record.Mark("StatusBitsADescription", record.SlotOffset, 1);

            record.RecordType = (RecordType)((statusA >> 1) & 7);

            record.HasNullBitmap = record.StatusBitsA[4];
            record.HasVariableLengthColumns = record.StatusBitsA[5];
        }

        private static void LoadIndexType(IndexRecord record)
        {
            if (record.Page.Header.IndexId > 0)
            {
                record.IndexType = record.IndexType | IndexTypes.NonClustered;
            }
            else
            {
                record.IndexType = record.IndexType | IndexTypes.Clustered;
            }

            if (record.Page.Header.Level > 0)
            {
                record.IndexType = record.IndexType | IndexTypes.Node;
            }
            else
            {
                record.IndexType = record.IndexType | IndexTypes.Leaf;
            }

            if ((record.Structure as IndexStructure).Heap)
            {
                record.IndexType = record.IndexType | IndexTypes.Heap;
            }
            else
            {
                record.IndexType = record.IndexType | IndexTypes.TableClustered;
            }

            record.IncludeKey = (!(record.Structure as IndexStructure).Unique
                                      && record.IsIndexType(IndexTypes.NonClustered))
                                      || record.IsIndexType(IndexTypes.NonClusteredLeaf);

        }
    }
}
