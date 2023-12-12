﻿using System;
using System.Collections;
using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Compressed;

namespace InternalsViewer.Internals.RecordLoaders;

/// <summary>
/// Loads a Compressed Data Record
/// </summary>
public class CompressedDataRecordLoader : RecordLoader
{
    public static void Load(CompressedDataRecord record)
    {
        record.ColumnCount = LoadNumberOfColumns(record);

        LoadStatus(record);

        if (record.RecordType == RecordType.Forwarding)
        {
            LoadForwardingRecord(record);
            return;
        }

        var cdArraySize = record.ColumnCount / 2 + record.ColumnCount % 2;

        LoadCdArray(record);

        record.CompressedSize = 0;

        for (var i = 0; i < record.ColumnCount; i++)
        {
            record.CompressedSize += Convert.ToInt16(GetCdArrayItemSize(record.GetCdByte(i)));
        }

        if (record.HasVariableLengthColumns)
        {
            //record.Unknown = record.Page.PageData[record.SlotOffset + 1 + record.ColumnCountBytes + cdArraySize + record.CompressedSize];

            var varLengthColumnCountOffset = record.SlotOffset + 2 + record.ColumnCountBytes + cdArraySize + record.CompressedSize;

            record.VariableLengthColumnCount = BitConverter.ToUInt16(record.Page.PageData, varLengthColumnCountOffset);

            record.MarkDataStructure("VariableLengthColumnCount", varLengthColumnCountOffset, sizeof(short));
        }

        LoadShortFields(record);

        if (record.VariableLengthColumnCount > 0 && record.HasVariableLengthColumns)
        {
            var colArrayOffset = record.SlotOffset + (5 + cdArraySize) + record.CompressedSize;

            record.ColOffsetArray = GetOffsetArray(record.Page.PageData,
                record.VariableLengthColumnCount,
                colArrayOffset);

            record.MarkDataStructure("ColOffsetArrayDescription", colArrayOffset, record.VariableLengthColumnCount * sizeof(short));
        }

        var longStartPos = record.SlotOffset + 4 + record.ColumnCountBytes + cdArraySize + record.CompressedSize + (2 * record.VariableLengthColumnCount);

        LoadLongFields(longStartPos, record);
    }

    internal static void LoadStatus(CompressedDataRecord record)
    {
        record.StatusBitsA = new BitArray(new[] { record.Page.PageData[record.SlotOffset] });

        record.RecordType = (RecordType)((record.Page.PageData[record.SlotOffset] >> 1) & 7);

        record.HasVariableLengthColumns = record.StatusBitsA[5];
        record.HasNullBitmap = record.StatusBitsA[4];

        record.MarkDataStructure("StatusBitsADescription", record.SlotOffset, sizeof(byte));
    }

    private static void LoadForwardingRecord(CompressedDataRecord record)
    {
        throw new NotImplementedException();
    }

    internal static short LoadNumberOfColumns(CompressedDataRecord record)
    {
        short columns;

        if ((record.Page.PageData[record.SlotOffset + 1] & 0x80) == 0x80)
        {
            // Check if the first bit is high, if it is it indicates 2-byte int
            record.ColumnCountBytes = 2;

            var noOfColumnsData = new byte[2];

            Array.Copy(record.Page.PageData, record.SlotOffset + 1, noOfColumnsData, 0, 2);

            noOfColumnsData[0] = Convert.ToByte(noOfColumnsData[0] ^ 0x80);

            Array.Reverse(noOfColumnsData);

            columns = BitConverter.ToInt16(noOfColumnsData, 0);
        }
        else
        {
            record.ColumnCountBytes = 1;

            columns = record.Page.PageData[record.SlotOffset + 1];
        }

        // 1/2 byte int located after the status bits (1 byte)
        record.MarkDataStructure("ColumnCount", record.SlotOffset + sizeof(byte), record.ColumnCountBytes);

        return columns;
    }

    public static void LoadShortFields(CompressedDataRecord record)
    {
        LoadShortFields(record, false);
    }

    public static void LoadShortFields(CompressedDataRecord record, bool hasDownPagePointer)
    {
        int offset;

        var index = 0;

        offset = record.SlotOffset + 1 + record.ColumnCountBytes + (record.ColumnCount / 2) + (record.ColumnCount % 2);

        for (var i = 0; i < record.ColumnCount; i++)
        {
            if (record.GetCdByte(i) != 10)
            {
                CompressedRecordField field;

                field = new CompressedRecordField(record.Structure.Columns[i], record);
                field.Compressed = true;

                if (field.Column.DataType == SqlDbType.Bit)
                {
                    field.Length = 1;
                    field.Compressed = true;
                    field.Data = new[] { record.GetCdByte(i) };
                }
                else
                {
                    var size = GetCdArrayItemSize(record.GetCdByte(i));

                    field.IsNull = (record.GetCdByte(i) == 0);
                    field.Length = size;

                    field.PageSymbol = (record.GetCdByte(i) > 10);

                    if (size > 0)
                    {
                        field.Data = new byte[size];
                        Array.Copy(record.Page.PageData, offset, field.Data, 0, size);

                        field.Offset = offset;
                        offset += size;
                    }
                }

                if (record.Page.CompressionInformation != null && record.Page.CompressionInformation.AnchorRecord != null)
                {
                    field.AnchorField = record.Page.CompressionInformation.AnchorRecord.Fields.Find(
                        f => f.Column.ColumnId == i + 1);
                }

                field.MarkDataStructure("Value", field.Offset, field.Length);

                record.MarkDataStructure("FieldsArray", field.Name, index);

                index++;

                record.Fields.Add(field);
            }
        }
    }

    public static void LoadLongFields(int startPos, CompressedDataRecord record)
    {
        var longColIndex = 0;
        var prevLength = 0;

        for (var i = 0; i < record.ColumnCount; i++)
        {
            if (record.GetCdByte(i) == 10)
            {
                var field = new CompressedRecordField(record.Structure.Columns[i], record);

                field.Length = DecodeOffset(record.ColOffsetArray[longColIndex]) - prevLength;
                field.Data = new byte[field.Length];
                field.Offset = startPos + prevLength;

                var isLob = (record.ColOffsetArray[longColIndex] & 0x8000) == 0x8000;

                Array.Copy(record.Page.PageData, field.Offset, field.Data, 0, field.Length);

                if (record.Page.CompressionInformation != null && record.Page.CompressionInformation.AnchorRecord != null)
                {
                    field.AnchorField = record.Page.CompressionInformation.AnchorRecord.Fields.Find(
                        f => f.Column.ColumnId == i);
                }

                record.Fields.Add(field);

                if (isLob)
                {
                    LoadLobField(field, field.Data, field.Offset);
                }
                else
                {
                    field.MarkDataStructure("Value", field.Offset, field.Length);
                }

                record.MarkDataStructure("FieldsArray", field.Name, record.Fields.Count - 1);

                prevLength = DecodeOffset(record.ColOffsetArray[longColIndex]);

                longColIndex++;
            }
        }
    }

    public static int GetCdArrayItemSize(int cdItem)
    {
        if (cdItem is > 0 and < 10)
        {
            return cdItem - 1;
        }

        if (cdItem > 10)
        {
            return cdItem - 11;
        }

        return 0;
    }

    /// <summary>
    /// Loads the CD (column descriptor) Array.
    /// </summary>
    private static void LoadCdArray(CompressedDataRecord record)
    {
        var bytePos = 1 + record.ColumnCountBytes;

        for (var i = 0; i < Math.Ceiling(record.ColumnCount / 2D); i++)
        {
            record.MarkDataStructure("CdItemsArray", "CD Array offset " + i, i);

            var cdItem = Convert.ToByte(record.Page.PageData[record.SlotOffset + bytePos]);

            var item = new CdArray(i, cdItem);

            item.MarkDataStructure("Description", record.SlotOffset + bytePos, sizeof(byte));

            record.CdItems.Add(item);

            bytePos++;
        }
    }
}