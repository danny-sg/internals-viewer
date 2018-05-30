using System;
using System.Collections;
using System.Collections.Generic;
using InternalsViewer.Internals.BlobPointers;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.RecordLoaders
{
    /// <summary>
    /// Loads a DataRecord object
    /// </summary>
    public class DataRecordLoader : RecordLoader
    {
        /// <summary>
        /// Loads the specified data record.
        /// </summary>
        /// <param name="dataRecord">The data record.</param>
        internal static void Load(DataRecord dataRecord)
        {
            if (dataRecord.Structure.Columns.Count == 0)
            {
                return;
            }

            dataRecord.NullBitmapSize = (short)((dataRecord.Structure.Columns.FindAll(column => !column.Sparse).Count - 1) / 8 + 1);

            if (LoadStatusBits(dataRecord) == RecordType.Forwarding)
            {
                LoadForwardingRecord(dataRecord);
                return;
            }

            // Fixed column offset 2-byte int located after Status Bits A (1 byte) and Status Bits B (1 byte)
            var columnCountOffsetPosition = dataRecord.SlotOffset + sizeof(byte) + sizeof(byte);

            dataRecord.ColumnCountOffset = BitConverter.ToInt16(dataRecord.Page.PageData, columnCountOffsetPosition);

            dataRecord.Mark("ColumnCountOffset", columnCountOffsetPosition, sizeof(short));

            // Column count 2-byte int located at the column count offset
            var columnCountPosition = dataRecord.SlotOffset + dataRecord.ColumnCountOffset;

            dataRecord.ColumnCount = BitConverter.ToInt16(dataRecord.Page.PageData, columnCountPosition);

            dataRecord.Mark("ColumnCount", columnCountPosition, sizeof(short));

            if (dataRecord.HasNullBitmap)
            {
                LoadNullBitmap(dataRecord);
            }

            short offsetStart = 0;

            if (dataRecord.HasVariableLengthColumns && !dataRecord.Compressed)
            {
                if (dataRecord.Structure.HasSparseColumns
                    && dataRecord.Structure.Columns.FindAll(column => !column.Sparse).Count == 0)
                {
                    dataRecord.VariableLengthColumnCount = 1;

                    offsetStart = (short)(dataRecord.ColumnCountOffset + sizeof(byte) + dataRecord.NullBitmapSize);
                }
                else
                {
                    // Number of variable length columns (2-byte int) located after null bitmap
                    var varColCountOffset = dataRecord.ColumnCountOffset + sizeof(short) + dataRecord.NullBitmapSize;

                    dataRecord.VariableLengthColumnCount = BitConverter.ToUInt16(dataRecord.Page.PageData, dataRecord.SlotOffset + varColCountOffset);

                    dataRecord.Mark("VariableLengthColumnCount", dataRecord.SlotOffset + varColCountOffset, sizeof(short));

                    // Offset starts after the variable length column count (2-bytes)
                    offsetStart = (short)(varColCountOffset + sizeof(short));
                }

                // Load offset array of 2-byte ints indicating the end offset of each variable length field
                dataRecord.ColOffsetArray = GetOffsetArray(dataRecord.Page.PageData,
                                                           dataRecord.VariableLengthColumnCount,
                                                           dataRecord.SlotOffset + offsetStart);

                dataRecord.Mark("ColOffsetArrayDescription", dataRecord.SlotOffset + offsetStart, dataRecord.VariableLengthColumnCount * sizeof(short));

            }
            else
            {
                dataRecord.VariableLengthColumnCount = 0;
                dataRecord.ColOffsetArray = null;
            }

            // Varible length data starts after the offset array length (2 byte ints * number of variable length columns)
            dataRecord.VariableLengthDataOffset = (ushort)(offsetStart + (sizeof(ushort) * dataRecord.VariableLengthColumnCount));

            LoadColumnValues(dataRecord);

            if (dataRecord.Structure.HasSparseColumns)
            {
                LoadSparseVector(dataRecord);
            }
        }

        /// <summary>
        /// Loads the null bitmap values
        /// </summary>
        /// <param name="dataRecord">The data record.</param>
        private static void LoadNullBitmap(DataRecord dataRecord)
        {
            var nullBitmapBytes = new byte[dataRecord.NullBitmapSize];

            // Null bitmap located after column count offset + column count 2-byte int
            var nullBitmapPosition = dataRecord.SlotOffset + dataRecord.ColumnCountOffset + sizeof(short);

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

            endOffset = DecodeOffset(dataRecord.ColOffsetArray[dataRecord.ColOffsetArray.Length - 1]);

            var sparseRecord = new byte[endOffset - startOffset];

            Array.Copy(dataRecord.Page.PageData, dataRecord.SlotOffset + startOffset, sparseRecord, 0, endOffset - startOffset);

            dataRecord.Mark("SparseVector");

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

            var columnValues = new List<RecordField>();

            var index = 0;

            foreach (var column in dataRecord.Structure.Columns)
            {
                if (!column.Sparse)
                {
                    field = new RecordField(column);

                    short length = 0;
                    ushort offset = 0;
                    var isLob = false;
                    byte[] data = null;
                    ushort variableIndex = 0;

                    if (column.Sparse)
                    {
                        // Can't remember what needs to happen here, will fix when I revisit the sparse column support
                    }
                    else if (column.LeafOffset >= 0 && column.LeafOffset < Page.Size)
                    {
                        // Fixed length field

                        // Fixed offset given by the column leaf offset field in sys.columns
                        offset = (ushort)column.LeafOffset;

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
                        variableIndex = (ushort)((column.LeafOffset * -1) - 1);

                        if (variableIndex == 0)
                        {
                            // If it's position 0 the start of the data will be at the variable length data offset...
                            offset = dataRecord.VariableLengthDataOffset;
                        }
                        else
                        {
                            // ...else use the end offset of the previous column as the start of this one
                            offset = DecodeOffset(dataRecord.ColOffsetArray[variableIndex - 1]);
                        }

                        if (variableIndex < dataRecord.ColOffsetArray.Length)
                        {
                            isLob = (dataRecord.ColOffsetArray[variableIndex] & 0x8000) == 0x8000;
                            length = (short)(DecodeOffset(dataRecord.ColOffsetArray[variableIndex]) - offset);
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

                    if (isLob)
                    {
                        LoadLobField(field, data, dataRecord.SlotOffset + offset);
                    }
                    else
                    {
                        field.Mark("Value", dataRecord.SlotOffset + field.Offset, field.Length);
                    }

                    dataRecord.Mark("FieldsArray", field.Name, index);

                    index++;

                    columnValues.Add(field);
                }
            }

            dataRecord.Fields.AddRange(columnValues);
        }

        /// <summary>
        /// Loads the status bits.
        /// </summary>
        /// <param name="record">The data record.</param>
        /// <returns>The Record Type property in Status Bits A</returns>
        private static RecordType LoadStatusBits(DataRecord record)
        {
            var statusA = record.Page.PageData[record.SlotOffset];

            // bytes 0 and 1 are Status Bits A and B
            record.StatusBitsA = new BitArray(new[] { statusA });
            record.StatusBitsB = new BitArray(new[] { record.Page.PageData[record.SlotOffset + 1] });

            record.Mark("StatusBitsADescription", record.SlotOffset, sizeof(byte));

            record.RecordType = (RecordType)((statusA >> 1) & 7);

            if (record.RecordType == RecordType.Forwarding)
            {
                return record.RecordType;
            }

            record.HasNullBitmap = record.StatusBitsA[4];
            record.HasVariableLengthColumns = record.StatusBitsA[5];

            record.Mark("StatusBitsBDescription", record.SlotOffset + sizeof(byte), sizeof(byte));

            return record.RecordType;
        }

        /// <summary>
        /// Loads a forwarding record.
        /// </summary>
        /// <param name="dataRecord">The data record.</param>
        private static void LoadForwardingRecord(DataRecord dataRecord)
        {
            var forwardingRecord = new byte[8];

            Array.Copy(dataRecord.Page.PageData, dataRecord.SlotOffset + sizeof(byte), forwardingRecord, 0, 6);

            dataRecord.ForwardingRecord = new RowIdentifier(forwardingRecord);

            dataRecord.Mark("ForwardingRecord", dataRecord.SlotOffset + sizeof(byte), 6);
        }
    }
}
