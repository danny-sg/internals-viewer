
using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.BlobPointers;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Structures;
namespace InternalsViewer.Internals.RecordLoaders
{
    public class DataRecordLoader : RecordLoader
    {
        internal static void Load(DataRecord dataRecord)
        {
            if (dataRecord.Structure.Columns.Count == 0)
            {
                return;
            }

            dataRecord.NullBitmapSize = (Int16)((dataRecord.Structure.Columns.FindAll(delegate(Column column)
                                                 {
                                                     return column.Sparse == false;
                                                 }).Count - 1) / 8 + 1);

            if (LoadStatusBits(dataRecord) == RecordType.Forwarding)
            {
                LoadForwardingRecord(dataRecord);
                return;
            }

            // Fixed column offset 2-byte int located after Status Bits A (1 byte) and Status Bits B (1 byte)
            int columnCountOffsetPosition = dataRecord.SlotOffset + sizeof(byte) + sizeof(byte);

            dataRecord.ColumnCountOffset = BitConverter.ToInt16(dataRecord.Page.PageData, columnCountOffsetPosition);

            dataRecord.Mark("ColumnCountOffset", columnCountOffsetPosition, sizeof(Int16));

            // Column count 2-byte int located at the column count offset
            int columnCountPosition = dataRecord.SlotOffset + dataRecord.ColumnCountOffset;

            dataRecord.ColumnCount = BitConverter.ToInt16(dataRecord.Page.PageData,columnCountPosition);

            dataRecord.Mark("ColumnCount", columnCountPosition, sizeof(Int16));

            if (dataRecord.HasNullBitmap)
            {
                LoadNullBitmap(dataRecord);
            }

            Int16 offsetStart = 0;

            if (dataRecord.HasVariableLengthColumns && !dataRecord.Compressed)
            {
                if (dataRecord.Structure.HasSparseColumns
                    && dataRecord.Structure.Columns.FindAll(delegate(Column column) { return column.Sparse == false; }).Count == 0)
                {
                    dataRecord.VariableLengthColumnCount = 1;

                    offsetStart = (Int16)(dataRecord.ColumnCountOffset + sizeof(byte) + dataRecord.NullBitmapSize);
                }
                else
                {
                    // Number of variable length columns (2-byte int) located after null bitmap
                    int varColCountOffset = dataRecord.ColumnCountOffset + sizeof(Int16) + dataRecord.NullBitmapSize;

                    dataRecord.VariableLengthColumnCount = BitConverter.ToUInt16(dataRecord.Page.PageData, dataRecord.SlotOffset + varColCountOffset);

                    dataRecord.Mark("VariableLengthColumnCount", dataRecord.SlotOffset + varColCountOffset, sizeof(Int16));

                    // Offset starts after the variable length column count (2-bytes)
                    offsetStart = (Int16)(varColCountOffset + sizeof(Int16));
                }

                // Load offset array of 2-byte ints indicating the end offset of each variable length field
                dataRecord.ColOffsetArray = GetOffsetArray(dataRecord.Page.PageData,
                                                           dataRecord.VariableLengthColumnCount,
                                                           dataRecord.SlotOffset + offsetStart);

                dataRecord.Mark("ColOffsetArrayDescription", dataRecord.SlotOffset + offsetStart, dataRecord.VariableLengthColumnCount * sizeof(Int16));

            }
            else
            {
                dataRecord.VariableLengthColumnCount = 0;
                dataRecord.ColOffsetArray = null;
            }

            // Varible length data starts after the offset array length (2 byte ints * number of variable length columns)
            dataRecord.VariableLengthDataOffset = (UInt16)(offsetStart + (sizeof(UInt16) * dataRecord.VariableLengthColumnCount));

            DataRecordLoader.LoadColumnValues(dataRecord);

            if (dataRecord.Structure.HasSparseColumns)
            {
                DataRecordLoader.LoadSparseVector(dataRecord);
            }
        }

        /// <summary>
        /// Loads the null bitmap values
        /// </summary>
        /// <param name="dataRecord">The data record.</param>
        private static void LoadNullBitmap(DataRecord dataRecord)
        {
            byte[] nullBitmapBytes = new byte[dataRecord.NullBitmapSize];

            // Null bitmap located after column count offset + column count 2-byte int
            int nullBitmapPosition = dataRecord.SlotOffset + dataRecord.ColumnCountOffset + sizeof(Int16);

            Array.Copy(dataRecord.Page.PageData,
                       nullBitmapPosition,
                       nullBitmapBytes,
                       0,
                       dataRecord.NullBitmapSize);

            dataRecord.NullBitmap = new BitArray(nullBitmapBytes);

            dataRecord.Mark("NullBitmapDescription", nullBitmapPosition, dataRecord.NullBitmapSize);
        }

        /// <summary>
        /// Loads the sparse vector.
        /// </summary>
        /// <param name="dataRecord">The data record.</param>
        private static void LoadSparseVector(DataRecord dataRecord)
        {
            int startOffset;
            int endOffset;

            if (dataRecord.ColOffsetArray.Length == 1)
            {
                startOffset = dataRecord.VariableLengthDataOffset;
            }
            else
            {
                startOffset = dataRecord.ColOffsetArray[dataRecord.ColOffsetArray.Length - 2];
            }

            endOffset = RecordLoader.DecodeOffset(dataRecord.ColOffsetArray[dataRecord.ColOffsetArray.Length - 1]);

            byte[] sparseRecord = new byte[endOffset - startOffset];

            Array.Copy(dataRecord.Page.PageData, dataRecord.SlotOffset + startOffset, sparseRecord, 0, endOffset - startOffset);

            dataRecord.SparseVector = new SparseVector(sparseRecord, (TableStructure)dataRecord.Structure, dataRecord, (short)startOffset);
        }

        /// <summary>
        /// Loads the column values.
        /// </summary>
        /// <param name="dataRecord">The data record.</param>
        /// <returns></returns>
        private static void LoadColumnValues(DataRecord dataRecord)
        {
            RecordField field;

            List<RecordField> columnValues = new List<RecordField>();

            int index = 0;

            foreach (Column column in dataRecord.Structure.Columns)
            {
                if (!column.Sparse)
                {
                    field = new RecordField(column);

                    Int16 length = 0;
                    UInt16 offset = 0;
                    bool isLob = false;
                    byte[] data = null;
                    UInt16 variableIndex = 0;

                    if (column.Sparse)
                    {
                        // Can't remember what needs to happen here, will fix when I revisit the sparse column support
                    }
                    else if (column.LeafOffset >= 0 && column.LeafOffset < 8192)
                    {
                        // Fixed length field

                        // Fixed offset given by the column leaf offset field in sys.columns
                        offset = (UInt16)column.LeafOffset;

                        // Length fixed from data type/data length
                        length = column.DataLength;

                        data = new byte[length];

                        Array.Copy(dataRecord.Page.PageData, column.LeafOffset + dataRecord.SlotOffset, data, 0, length);
                    }
                    else if (dataRecord.HasVariableLengthColumns && dataRecord.HasNullBitmap && !column.Dropped
                             && (column.ColumnId < 0 || !dataRecord.NullBitmapValue(column)))
                    {
                        // Variable Length fields

                        // Use the leaf offset to get the variable length column ordinal
                        variableIndex = (UInt16)((column.LeafOffset * -1) - 1);

                        if (variableIndex == 0)
                        {
                            // If it's position 0 the start of the data will be at the variable length data offset...
                            offset = dataRecord.VariableLengthDataOffset;
                        }
                        else
                        {
                            if (variableIndex < dataRecord.ColOffsetArray.Length)
                            {
                                // ...else use the end offset of the previous column as the start of this one
                                offset = RecordLoader.DecodeOffset(dataRecord.ColOffsetArray[variableIndex - 1]);
                            }
                        }

                        if (variableIndex < dataRecord.ColOffsetArray.Length)
                        {
                            isLob = (dataRecord.ColOffsetArray[variableIndex] & 0x8000) == 0x8000;
                            length = (Int16)(RecordLoader.DecodeOffset(dataRecord.ColOffsetArray[variableIndex]) - offset);
                        }
                        else
                        {
                            isLob = false;
                            length = 0;
                        }

                        data = new byte[length];

                        Array.Copy(dataRecord.Page.PageData, dataRecord.SlotOffset + offset, data, 0, length);
                    }

                    field.Offset = offset;
                    field.Length = length;
                    field.Data = data;
                    field.VariableOffset = variableIndex;

                    dataRecord.Mark("FieldsArray", dataRecord.SlotOffset + field.Offset, field.Length, index);
                    index++;

                    if (isLob)
                    {
                        LoadLobField(field, data);
                    }

                    columnValues.Add(field);
                }
            }

            dataRecord.Fields.AddRange(columnValues);
        }

        /// <summary>
        /// Loads a LOB field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="data">The data.</param>
        private static void LoadLobField(RecordField field, byte[] data)
        {
            // First byte gives the Blob field type
            switch ((BlobFieldType)data[0])
            {
                case BlobFieldType.LobPointer:

                    field.BlobInlineRoot = new PointerField(data);
                    break;

                case BlobFieldType.LobRoot:

                    field.BlobInlineRoot = new RootField(data);
                    break;

                case BlobFieldType.RowOverflow:

                    field.BlobInlineRoot = new OverflowField(data);
                    break;
            }
        }

        /// <summary>
        /// Loads the status bits.
        /// </summary>
        /// <param name="dataRecord">The data record.</param>
        /// <returns>The Record Type property in Status Bits A</returns>
        private static RecordType LoadStatusBits(DataRecord dataRecord)
        {
            byte statusA = dataRecord.Page.PageData[dataRecord.SlotOffset];

            // bytes 0 and 1 are Status Bits A and B
            dataRecord.StatusBitsA = new BitArray(new byte[] { statusA });
            dataRecord.StatusBitsB = new BitArray(new byte[] { dataRecord.Page.PageData[dataRecord.SlotOffset + 1] });

            dataRecord.Mark("StatusBitsADescription", dataRecord.SlotOffset, 1);

            dataRecord.RecordType = (RecordType)((statusA >> 1) & 7);

            if (dataRecord.RecordType == RecordType.Forwarding)
            {
                return dataRecord.RecordType;
            }

            dataRecord.HasNullBitmap = dataRecord.StatusBitsA[4];
            dataRecord.HasVariableLengthColumns = dataRecord.StatusBitsA[5];

            dataRecord.Mark("StatusBitsBDescription", dataRecord.SlotOffset + sizeof(byte), 1);

            return dataRecord.RecordType;
        }

        /// <summary>
        /// Loads a forwarding record.
        /// </summary>
        /// <param name="dataRecord">The data record.</param>
        private static void LoadForwardingRecord(DataRecord dataRecord)
        {
            byte[] forwardingRecord = new byte[8];

            Array.Copy(dataRecord.Page.PageData, dataRecord.SlotOffset + 1, forwardingRecord, 0, 6);

            RecordField field = new RecordField(null);

            field.Data = forwardingRecord;

            dataRecord.Fields.Add(field);
        }
    }
}
