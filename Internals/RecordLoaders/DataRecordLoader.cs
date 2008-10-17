
using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.BlobPointers;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Structures;
namespace InternalsViewer.Internals.RecordLoaders
{
    class DataRecordLoader : RecordLoader
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

            dataRecord.FixedColumnDataOffset = BitConverter.ToInt16(dataRecord.Page.PageData,
                                                                    dataRecord.SlotOffset + 2);

            dataRecord.ColumnCount = BitConverter.ToInt16(dataRecord.Page.PageData,
                                                          dataRecord.SlotOffset + dataRecord.FixedColumnDataOffset);

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

                    offsetStart = (Int16)(dataRecord.FixedColumnDataOffset + 1 + dataRecord.NullBitmapSize);
                }
                else
                {
                    int varColCountOffset = dataRecord.SlotOffset + dataRecord.FixedColumnDataOffset + 2 + dataRecord.NullBitmapSize;

                    dataRecord.VariableLengthColumnCount = BitConverter.ToUInt16(dataRecord.Page.PageData, varColCountOffset);

                    offsetStart = (Int16)(dataRecord.FixedColumnDataOffset + 4 + dataRecord.NullBitmapSize);
                }

                dataRecord.ColOffsetArray = GetOffsetArray(dataRecord.Page.PageData,
                                                           dataRecord.VariableLengthColumnCount,
                                                           dataRecord.SlotOffset + offsetStart);
            }
            else
            {
                dataRecord.VariableLengthColumnCount = 0;
                dataRecord.ColOffsetArray = null;
            }

            dataRecord.VariableLengthDataOffset = (UInt16)(offsetStart + (2 * dataRecord.VariableLengthColumnCount));

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

            Array.Copy(dataRecord.Page.PageData,
                       dataRecord.SlotOffset + dataRecord.FixedColumnDataOffset + 2,
                       nullBitmapBytes,
                       0,
                       dataRecord.NullBitmapSize);

            dataRecord.NullBitmap = new BitArray(nullBitmapBytes);
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

            foreach (Column column in dataRecord.Structure.Columns)
            {
                if (!column.Sparse)
                {
                    field = new RecordField(column);

                    UInt16 length = 0;
                    UInt16 offset = 0;
                    bool isLob = false;
                    byte[] data = null;
                    UInt16 variableIndex = 0;

                    if (column.Sparse)
                    {
                    }
                    else if (column.LeafOffset >= 0 && column.LeafOffset < 8192) // Fixed length field
                    {
                        offset = column.LeafOffset;
                        length = column.DataLength;

                        data = new byte[length];

                        Array.Copy(dataRecord.Page.PageData, column.LeafOffset + dataRecord.SlotOffset, data, 0, length);
                    }
                    else if (dataRecord.HasVariableLengthColumns && dataRecord.HasNullBitmap && !column.Dropped 
                             && (column.ColumnId < 0 || !dataRecord.NullBitmapValue(column)))
                    {
                        //TODO: Clean up the logic here
                        variableIndex = (UInt16)((column.LeafOffset * -1) - 1);

                        if (variableIndex == 0)
                        {
                            offset = dataRecord.VariableLengthDataOffset;
                        }
                        else
                        {
                            if (variableIndex < dataRecord.ColOffsetArray.Length)
                            {
                                offset = RecordLoader.DecodeOffset(dataRecord.ColOffsetArray[variableIndex - 1]);
                            }
                        }

                        if (variableIndex < dataRecord.ColOffsetArray.Length)
                        {
                            isLob = (dataRecord.ColOffsetArray[variableIndex] & 0x8000) == 0x8000;
                            length = (UInt16)(RecordLoader.DecodeOffset(dataRecord.ColOffsetArray[variableIndex]) - offset);
                        }
                        else
                        {
                            isLob = false;
                            length = 0;
                        }

                        data = new byte[length];

                        Array.Copy(dataRecord.Page.PageData, offset + dataRecord.SlotOffset, data, 0, length);
                    }

                    field.Offset = offset;
                    field.Length = length;
                    field.Data = data;
                    field.VariableOffset = variableIndex;

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
        /// <returns></returns>
        private static RecordType LoadStatusBits(DataRecord dataRecord)
        {
            byte statusA = dataRecord.Page.PageData[dataRecord.SlotOffset];

            dataRecord.StatusBitsA = new BitArray(new byte[] { statusA });
            dataRecord.StatusBitsB = new BitArray(new byte[] { dataRecord.Page.PageData[dataRecord.SlotOffset + 1] });

            dataRecord.RecordType = (RecordType)((statusA >> 1) & 7);

            if (dataRecord.RecordType == RecordType.Forwarding)
            {
                return dataRecord.RecordType;
            }

            dataRecord.HasVariableLengthColumns = dataRecord.StatusBitsA[5];
            dataRecord.HasNullBitmap = dataRecord.StatusBitsA[4];

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
