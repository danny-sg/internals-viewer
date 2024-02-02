using System.Data;
using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Annotations;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Services.Loaders.Records;

/// <summary>
/// Loader for records in the CD (Column Descriptor) format
/// </summary>
/// <remarks>
/// CD format is used for compressed records and is a different format to the standard FixedVar record format.
/// 
/// Every column has a CD (Column Descriptor) byte which describes the column data.
/// 
/// The layout is:
/// 
///     - Header
///         1 byte bit array setting flags for the row
///         
///     - CD (Column Descriptor) Region
///         Either 1 or 2 bytes for the number of columns
///         
///         Then 4 bits per column to describe the length of the column data either specifically for the short region or generally for the 
///         long region
///         
///     - Short Data Region 
///         The short data region is for values where the length has been specified in the CD region.
///         
///         - Short Data Cluster Array
///         
///             Short columns are grouped into clusters of 30. The short data cluster array describes the length of each cluster, a byte 
///             for each length that can be used to find the offset. This can be used to shortcut finding the column data
///             
///             The size of the array is (Short Column Count - 1) / 30.
///             
///             The first cluster is directly after the cluster array, so less than 30 columns and there is no array.
///     
///     - Long Data Region
///         Data longer than 8 bytes.
///         
///         The region has three parts:
///         
///         1. Offset Array
///         
///             - Header - 1 byte 
///             - Entry Count - 2 bytes
///             - Offset Entry Array - 2 bytes per offset
///             
///         2. Long Data Cluster Array
///         
///         3. Long Data
///         
///         Defined by the offset array
///         
///     - Special Information 
///     
///         Indicated by the header byte. It could be a forwarding pointer, back pointer, or version info.
/// </remarks>
public class CdDataRecordLoader(ILogger<CdDataRecordLoader> logger)
{
    /// <summary>
    /// Columns are grouped into clusters of 30 in the short and long data regions
    /// </summary>
    private const int ClusterSize = 30;

    private ILogger<CdDataRecordLoader> Logger { get; } = logger;

    public CompressedDataRecord Load(AllocationUnitPage page, ushort slotOffset, TableStructure structure)
    {
        int currentPosition = slotOffset;

        var record = new CompressedDataRecord(page.CompressionInfo!)
        {
            Offset = slotOffset,
            RowIdentifier = new RowIdentifier(page.PageAddress, slotOffset)
        };

        LoadHeader(record, page.Data, currentPosition);

        currentPosition += sizeof(byte);

        if (record.RecordType == CompressedRecordType.Forwarding)
        {
            LoadForwardingRecord(record);

            return record;
        }

        var columnCountSize = LoadColumnCount(record, page.Data, currentPosition);

        record.MarkProperty(nameof(record.ColumnCount), currentPosition, columnCountSize);

        currentPosition += columnCountSize;

        LoadColumnDescriptorArray(record, page.Data, currentPosition);

        var columnDescriptorArraySize = (int)Math.Ceiling(record.ColumnCount / 2.0);

        record.MarkProperty(nameof(record.ColumnDescriptors), currentPosition, columnDescriptorArraySize);

        currentPosition += (int)Math.Ceiling(record.ColumnCount / 2.0);

        LoadShortDataRegion(record, structure, page.CompressionInfo?.AnchorRecord, page.Data, currentPosition);

        if (record.HasLongDataRegion)
        {
            // Calculate the size of the short data region
            currentPosition += record.ColumnDescriptors.Sum(c => c.Size);

            LoadLongDataRegion(record, structure, page.CompressionInfo?.AnchorRecord, page.Data, currentPosition);
        }

        return record;
    }

    /// <summary>
    /// Loads Short Data Region
    /// </summary>
    /// <remarks>
    /// Short Data Region:
    /// 
    ///     Short Data Cluster Array | Short Fields
    /// </remarks>
    private void LoadShortDataRegion(CompressedDataRecord record,
                                     TableStructure structure,
                                     CompressedDataRecord? anchorRecord,
                                     byte[] data,
                                     int offset)
    {
        var currentPosition = offset;

        ParseShortDataClusterArray(record, data, currentPosition);

        currentPosition += record.ShortDataClusterArray.Length;

        LoadShortFields(record, structure, anchorRecord, data, currentPosition);
    }

    /// <summary>
    /// Loads Long Data Region
    /// </summary>
    /// <remarks>
    /// Short Data Region:
    /// 
    ///     Long Data Header | Long Data Offset Count | Long Data Offset Array | Long Data Cluster Array | Long Data
    /// </remarks>
    private void LoadLongDataRegion(CompressedDataRecord record,
                                    TableStructure structure,
                                    CompressedDataRecord? anchorRecord,
                                    byte[] data,
                                    int offset)
    {
        var currentPosition = offset;

        record.LongDataHeader = data[currentPosition];

        record.MarkProperty(nameof(record.LongDataHeader), currentPosition, sizeof(byte));

        currentPosition += sizeof(byte);

        record.LongDataOffsetCount = BitConverter.ToUInt16(data, currentPosition);

        record.MarkProperty(nameof(record.LongDataOffsetCount), currentPosition, sizeof(short));

        currentPosition += sizeof(ushort);

        record.LongDataOffsetArray = RecordHelpers.GetOffsetArray(data, record.LongDataOffsetCount, currentPosition);

        record.MarkProperty(nameof(record.LongDataOffsetArray), currentPosition, record.LongDataOffsetCount * sizeof(ushort));

        currentPosition += record.LongDataOffsetCount * sizeof(ushort);

        ParseLongDataClusterArray(record, data, currentPosition);

        record.MarkProperty(nameof(record.LongDataClusterArray), currentPosition, record.LongDataClusterArray.Length);

        currentPosition += record.LongDataClusterArray.Length;

        LoadLongFields(currentPosition, record, anchorRecord, structure, data);
    }

    private void ParseLongDataClusterArray(CompressedDataRecord record, byte[] data, int offset)
    {
        var longDataClusterArraySize = (record.ColumnCount - 1) / ClusterSize;

        record.LongDataClusterArray = data[offset..(offset + longDataClusterArraySize)];

        if (longDataClusterArraySize > 0)
        {
            record.MarkProperty(nameof(record.LongDataClusterArray), offset, longDataClusterArraySize);
        }
    }

    private void ParseShortDataClusterArray(CompressedDataRecord record, byte[] data, int offset)
    {
        var shortDataClusterArraySize = (record.ColumnCount - 1) / ClusterSize;

        record.ShortDataClusterArray = data[offset..(offset + shortDataClusterArraySize)];

        if (shortDataClusterArraySize > 0)
        {
            record.MarkProperty(nameof(record.ShortDataClusterArray), offset, shortDataClusterArraySize);
        }
    }

    /// <summary>
    /// Loads the header byte
    /// </summary>
    /// <remarks>
    /// Bit 0 - CD Record Field Flag
    /// Bit 1 - If the record uses row versioning
    /// Bit 2 - 4 - Record Type - <see cref="CompressedRecordType"/>"
    /// Bit 5 - If the record has a a long data region
    /// </remarks>
    public void LoadHeader(CompressedDataRecord record, byte[] data, int offset)
    {
        record.Header = data[offset];

        record.IsCompressedDataRecord = (record.Header & 0b00000001) != 0;
        record.HasVersioning = (record.Header & 0b00000010) != 0;
        record.HasLongDataRegion = (record.Header & 0b00100000) != 0;

        record.RecordType = (CompressedRecordType)((record.Header >> 2) & 7);

        var tags = new List<string>();

        tags.Add(record.RecordType.ToString());

        tags.AddIf("Compressed", record.IsCompressedDataRecord);
        tags.AddIf("Has Long Data Region", record.HasLongDataRegion);
        tags.AddIf("Has Versioning", record.HasVersioning);

        record.MarkProperty(nameof(record.Header), offset, sizeof(byte), tags);

        Logger.LogDebug("Record Type: {0}", record.RecordType);
    }

    private static void LoadForwardingRecord(CompressedDataRecord record)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Column Count
    /// </summary>
    /// <remarks>
    /// Either a 1 or 2 byte integer giving the total number of columns in the record.
    /// 
    /// The first bit indicates if the column count is 1 or 2 bytes. If it is set/high it is 2, else 1.
    /// </remarks>
    private static short LoadColumnCount(CompressedDataRecord record, byte[] data, int offset)
    {
        short size;

        if ((data[offset] & 0x80) != 0)
        {
            // Check if the first bit is high, if it is it indicates 2-byte int
            size = 2;

            var columnCount = new byte[2];

            Array.Copy(data, offset, columnCount, 0, 2);

            columnCount[0] = Convert.ToByte(columnCount[0] ^ 0x80);

            Array.Reverse(columnCount);

            record.ColumnCount = BitConverter.ToInt16(columnCount, 0);
        }
        else
        {
            size = 1;

            record.ColumnCount = data[offset];
        }

        return size;
    }

    private static void LoadShortFields(CompressedDataRecord record,
                                        TableStructure structure,
                                        CompressedDataRecord? anchorRecord,
                                        byte[] data,
                                        int offset)
    {
        for (var i = 0; i < record.ColumnCount; i++)
        {
            var cluster = i / ClusterSize;

            var columnDescriptor = record.ColumnDescriptors[i];

            if (columnDescriptor.Value != ColumnDescriptorFlag.Long)
            {
                var field = new CompressedRecordField(structure.Columns[i], record);

                field.Cluster = cluster;

                if (structure.Columns[i].DataType == SqlDbType.Bit)
                {
                    field.Length = 1;
                    field.Data = new[] { (byte)record.ColumnDescriptors[i].Value };
                }
                else
                {
                    var size = columnDescriptor.Size;

                    field.IsNull = columnDescriptor.Value == ColumnDescriptorFlag.Null;
                    field.Length = size;

                    field.IsPageSymbol = columnDescriptor.Value == ColumnDescriptorFlag.PageSymbol;

                    if (size > 0)
                    {
                        field.Data = new byte[size];
                        Array.Copy(data, offset, field.Data, 0, size);

                        field.Offset = offset;
                        offset += size;
                    }
                }

                field.AnchorField = anchorRecord?.Fields
                                                 .Cast<CompressedRecordField>()
                                                 .FirstOrDefault(f => f.ColumnStructure.ColumnId == i + 1);

                record.MarkValue(ItemType.ShortFieldValue, field.Name, field, field.Offset, field.Length);

                record.Fields.Add(field);
            }
        }
    }

    private static void LoadLongFields(int offset,
                                       CompressedDataRecord record,
                                       CompressedDataRecord? anchorRecord,
                                       TableStructure structure,
                                       byte[] data)
    {
        var columnIndex = 0;
        var previousOffset = 0;

        for (var i = 0; i < record.ColumnCount; i++)
        {
            var cluster = i / ClusterSize;

            if (record.ColumnDescriptors[i].Value == ColumnDescriptorFlag.Long)
            {
                var nextOffset = record.LongDataOffsetArray[columnIndex];

                var field = new CompressedRecordField(structure.Columns[i], record);

                field.Cluster = cluster;
                field.Length = RecordHelpers.DecodeOffset(nextOffset) - previousOffset;
                field.Data = new byte[field.Length];
                field.Offset = offset + previousOffset;

                var isLob = (nextOffset & 0x8000) == 0;

                Array.Copy(data, field.Offset, field.Data, 0, field.Length);

                field.AnchorField = anchorRecord?.Fields
                                                 .Cast<CompressedRecordField>()
                                                 .FirstOrDefault(f => f.ColumnStructure.ColumnId == i);

                record.Fields.Add(field);

                if (isLob)
                {
                    // LoadLobField(field, field.Data, field.Offset);
                }

                record.MarkValue(ItemType.LongFieldValue, field.Name, field, field.Offset, field.Length);

                previousOffset = RecordHelpers.DecodeOffset(nextOffset);

                columnIndex++;
            }
        }
    }

    /// <summary>
    /// Loads the CD (column descriptor) array.
    /// </summary>
    /// <remarks>
    /// The CD array describes the length of each column in the record. Each column has a 4-bit value, a byte describes two columns.
    /// </remarks>
    private static void LoadColumnDescriptorArray(CompressedDataRecord record, byte[] data, int offset)
    {
        var columnDescriptors = new List<ColumnDescriptor>();
        var bytePosition = offset;

        var column = 1;

        while (column <= record.ColumnCount)
        {
            var byteValue = data[bytePosition];

            // Get the first four bits by masking with 15 (00001111)
            var value1 = (byte)(byteValue & 15);

            var item1 = new ColumnDescriptor(value1);

            item1.MarkValue(ItemType.ColumnDescriptor,
                            $"Column {column}",
                            value1,
                            bytePosition,
                            sizeof(byte),
                            new List<string> { item1.ToString() });

            columnDescriptors.Add(item1);

            column++;

            // If there are still columns to process, parse the next four bits
            if (column <= record.ColumnCount)
            {
                // Get the last four bits by shifting right 4 bits
                var value2 = (byte)(byteValue >> 4);

                var item2 = new ColumnDescriptor(value2);

                item2.MarkValue(ItemType.ColumnDescriptor,
                                $"Column {column}",
                                value2,
                                bytePosition,
                                sizeof(byte),
                                new List<string> { item2.ToString() });

                columnDescriptors.Add(item2);

                column++;
            }

            bytePosition++;
        }

        record.ColumnDescriptors = columnDescriptors.ToArray();
    }
}