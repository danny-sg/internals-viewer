using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Records;
using InternalsViewer.Internals.Structures;
using System.Collections;
using System.Data;

namespace InternalsViewer.Internals.RecordLoaders
{
    /// <summary>
    /// Loads a Compressed Data Record
    /// </summary>
    public class CompressedDataRecordLoader
    {
        public static void Load(CompressedDataRecord record)
        {
            record.ColumnCount = CompressedDataRecordLoader.LoadNumberOfColumns(record);

            LoadStatus(record);
         
            if (record.RecordType == RecordType.Forwarding)
            {
                CompressedDataRecordLoader.LoadForwardingRecord(record);
                return;
            }

            int cdArraySize = (int)(record.ColumnCount / 2 + record.ColumnCount % 2);

            CompressedDataRecordLoader.LoadCdArray(record);

            record.CompressedSize = 0;

            for (int i = 0; i < record.CdArray.Length; i++)
            {
                record.CompressedSize += Convert.ToInt16(CompressedDataRecordLoader.GetCdArrayItemSize(record.CdArray[i]));
            }

            if (record.HasVariableLengthColumns)
            {
               // record.unknown1 = record.Page.PageData[record.SlotOffset + 1 + record.ColumnCountBytes + cdArraySize + record.CompressedSize];

                record.VariableLengthColumnCount = BitConverter.ToUInt16(record.Page.PageData,
                                                              record.SlotOffset + 2 + record.ColumnCountBytes + cdArraySize + record.CompressedSize);
            }

            CompressedDataRecordLoader.LoadShortFields(record);

            if (record.VariableLengthColumnCount > 0 && record.HasVariableLengthColumns)
            {
                record.ColOffsetArray = RecordLoader.GetOffsetArray(record.Page.PageData,
                                                                    record.VariableLengthColumnCount,
                                                                    record.SlotOffset + (5 + cdArraySize) + record.CompressedSize);
            }

            int longStartPos = record.SlotOffset + 4 + record.ColumnCountBytes + cdArraySize + record.CompressedSize + (2 * record.VariableLengthColumnCount);

            CompressedDataRecordLoader.LoadLongFields(longStartPos, record);
        }

        internal static void LoadStatus(CompressedDataRecord record)
        {
            record.StatusBitsA = new BitArray(new byte[] { record.Page.PageData[record.SlotOffset] });

            record.RecordType = (RecordType)((record.Page.PageData[record.SlotOffset] >> 1) & 7);

            record.HasVariableLengthColumns = record.StatusBitsA[5];
            record.HasNullBitmap = record.StatusBitsA[4];
        }

        private static void LoadForwardingRecord(CompressedDataRecord record)
        {
            throw new NotImplementedException();
        }

        internal static Int16 LoadNumberOfColumns(CompressedDataRecord record)
        {
            if ((record.Page.PageData[record.SlotOffset + 1] & 0x80) == 0x80)
            {
                record.ColumnCountBytes = 2;

                byte[] noOfColumnsData = new byte[2];

                Array.Copy(record.Page.PageData, record.SlotOffset + 1, noOfColumnsData, 0, 2);

                noOfColumnsData[0] = Convert.ToByte(noOfColumnsData[0] ^ 0x80);

                Array.Reverse(noOfColumnsData);

                return BitConverter.ToInt16(noOfColumnsData, 0);
            }
            else
            {
                record.ColumnCountBytes = 1;

                return record.Page.PageData[record.SlotOffset + 1];
            }
        }

        public static void LoadShortFields(CompressedDataRecord record)
        {
            CompressedDataRecordLoader.LoadShortFields(record, false);
        }

        public static void LoadShortFields(CompressedDataRecord record, bool hasDownPagePointer)
        {
            int offset;

            offset = record.SlotOffset + 1 + record.ColumnCountBytes + (record.ColumnCount / 2) + (record.ColumnCount % 2);

            for (int i = 0; i < record.CdArray.Length; i++)
            {
                if (record.CdArray[i] != 10)
                {
                    CompressedRecordField field;

                    field = new CompressedRecordField(record.Structure.Columns[i], record);
                    field.Compressed = true;

                    if (field.Column.DataType == SqlDbType.Bit)
                    {
                        field.Length = 1;
                        field.Compressed = true;
                        field.Data = new byte[] { record.CdArray[i] };
                    }
                    else
                    {
                        int size = CompressedDataRecordLoader.GetCdArrayItemSize(record.CdArray[i]);

                        field.IsNull = record.CdArray[i] == 0;
                        field.Length = size;

                        field.PageSymbol = record.CdArray[i] > 10;

                        if (size > 0)
                        {
                            field.Data = new byte[size];
                            Array.Copy(record.Page.PageData, offset, field.Data, 0, size);
                            offset += size;
                        }
                    }

                    if (record.CompressionInfo != null && record.CompressionInfo.AnchorRecord != null)
                    {
                        field.AnchorField = record.CompressionInfo.AnchorRecord.Fields.Find(
                                                            delegate(RecordField f)
                                                            {
                                                                return f.Column.ColumnId == i;
                                                            });
                    }

                    record.Fields.Add(field);
                }
            }
        }

        public static void LoadLongFields(int startPos, CompressedDataRecord record)
        {
            int longColIndex = 0;

            int prevLength = 0;

            for (int i = 0; i < record.CdArray.Length; i++)
            {
                if (record.CdArray[i] == 10)
                {
                    CompressedRecordField field = new CompressedRecordField(record.Structure.Columns[i], record);

                    field.Length = record.ColOffsetArray[longColIndex] - prevLength;
                    field.Data = new byte[field.Length];

                    Array.Copy(record.Page.PageData, startPos + prevLength, field.Data, 0, field.Length);

                    if (record.CompressionInfo != null && record.CompressionInfo.AnchorRecord != null)
                    {
                        field.AnchorField = record.CompressionInfo.AnchorRecord.Fields.Find(
                                                            delegate(RecordField f)
                                                            {
                                                                return f.Column.ColumnId == i;
                                                            });
                    }

                    record.Fields.Add(field);

                    prevLength = record.ColOffsetArray[longColIndex];

                    longColIndex++;
                }
            }
        }

        public static int GetCdArrayItemSize(int cdItem)
        {
            if (cdItem > 0 && cdItem < 10)
            {
                return cdItem - 1;
            }
            else if (cdItem > 10)
            {
                return cdItem - 11;
            }
            else
            {
                return 0;
            }
        }

        private static void LoadCdArray(CompressedDataRecord record)
        {
            int bytePos = 1 + record.ColumnCountBytes;

            record.CdArray = new byte[record.ColumnCount];

            for (int i = 0; i < record.ColumnCount; i += 1)
            {
                if (i % 2 == 0)
                {
                    record.CdArray[i] = Convert.ToByte(record.Page.PageData[record.SlotOffset + bytePos] & 15);
                }
                else
                {
                    record.CdArray[i] = Convert.ToByte(record.Page.PageData[record.SlotOffset + bytePos] >> 4);
                    bytePos++;
                }
            }
        }
    }
}
