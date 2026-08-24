using System.Text;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Segments;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

internal static class ColumnstoreStructureDumper
{
    private const int SampleSize = 12;

    public static string DumpIndex(ColumnStoreIndex index)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Columnstore Index   {index.SchemaName}.{index.TableName}.{index.IndexName}");
        builder.AppendLine($"  objectId {index.ObjectId}   indexId {index.IndexId}   "
                           + $"{(index.IsClustered ? "clustered" : "nonclustered")}   hobt {index.HobtId}");
        builder.AppendLine($"  {index.Columns.Count} column(s)   {index.RowGroups.Count} row group(s)   "
                           + $"{index.TotalRows:N0} rows   {index.TotalSize:N0} bytes");

        builder.AppendLine();
        builder.AppendLine("Row Sets");

        foreach (var columnstoreRowset in index.Rowsets)
        {
            builder.AppendLine($"  {columnstoreRowset.RowsetType,-13} hobt {columnstoreRowset.HobtId}"
                               + $"{(columnstoreRowset.IsAllocated ? string.Empty : "   (not allocated)")}");

            foreach (var unit in columnstoreRowset.AllocationUnits)
            {
                builder.AppendLine($"    {unit.AllocationUnitType,-16} au {unit.AllocationUnitId,-20} "
                                   + $"first {unit.FirstPage,-12} root {unit.RootPage,-12} iam {unit.FirstIamPage,-12} "
                                   + $"used {unit.UsedPages,8:N0} total {unit.TotalPages,8:N0}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Entry Points");
        builder.AppendLine($"  Segment data     au {index.BlobAllocationUnit?.AllocationUnitId}   "
                           + $"first {index.FirstPage}   root {index.RootPage}   iam {index.FirstIamPage}");
        builder.AppendLine($"  Delete bitmap    au {index.DeleteBitmapAllocationUnit?.AllocationUnitId}   "
                           + $"first {index.DeleteBitmap?.FirstPage}   root {index.DeleteBitmap?.RootPage}   "
                           + $"iam {index.DeleteBitmap?.FirstIamPage}");
        builder.AppendLine($"  Delta stores     {index.DeltaStores.Count()}");

        foreach (var delta in index.DeltaStores)
        {
            builder.AppendLine($"    hobt {delta.HobtId}   au {delta.DataAllocationUnit?.AllocationUnitId}   "
                               + $"first {delta.FirstPage}   root {delta.RootPage}   iam {delta.FirstIamPage}");
        }

        builder.AppendLine();

        foreach (var rowGroup in index.RowGroups)
        {
            builder.AppendLine($"  Row group {rowGroup.RowGroupId,4}   {rowGroup.State,-11}   "
                               + $"rows {rowGroup.TotalRows,10:N0}   size {rowGroup.SizeInBytes,12:N0}   "
                               + $"deltaStore {rowGroup.DeltaStoreHobtId}");
        }

        return builder.ToString();
    }

    public static string DumpRowGroup(RowGroup rowGroup)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Row Group {rowGroup.RowGroupId}   state {rowGroup.State}   rows {rowGroup.TotalRows:N0}   "
                           + $"size {rowGroup.SizeInBytes:N0}   generation {rowGroup.Generation}");

        builder.AppendLine();

        builder.AppendLine($"{"Col",4} {"Name",-16} {"Encoding",-26} {"Rows",10} {"Size",12} {"BaseId",18} "
                           + $"{"Magnitude",14} {"MinDataId",20} {"MaxDataId",20} {"Dict",5}");

        foreach (var segment in rowGroup.Segments)
        {
            var dictionary = segment.SecondaryDictionaryId >= 0
                ? $"L{segment.SecondaryDictionaryId}"
                : segment.PrimaryDictionaryId >= 0
                    ? $"G{segment.PrimaryDictionaryId}"
                    : "-";

            builder.AppendLine($"{segment.Key.ColumnId,4} {Truncate(segment.Column?.Name, 16),-16} "
                               + $"{segment.Encoding,-26} {segment.RowCount,10:N0} {segment.OnDiskSize,12:N0} "
                               + $"{segment.BaseId,18} {segment.Magnitude,14} {segment.MinDataId,20} "
                               + $"{segment.MaxDataId,20} {dictionary,5}");
        }

        return builder.ToString();
    }

    public static string DumpSegment(ColumnSegment segment, SegmentBlob blob)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Segment  column {segment.Key.ColumnId} ({segment.Column?.Name})   "
                           + $"row group {segment.Key.RowGroupId}   encoding {segment.Encoding}");

        builder.AppendLine();
        builder.AppendLine("Header");
        builder.AppendLine($"  +0x00 Version              {blob.Header.Version}");
        builder.AppendLine($"  +0x04 LobType              {blob.Header.LobType} ({(int)blob.Header.LobType})");
        builder.AppendLine($"  +0x08 Reserved             {blob.Header.Reserved}");
        builder.AppendLine($"  +0x0C Unknown              {blob.Header.Unknown0C} (0x{blob.Header.Unknown0C:X})");
        builder.AppendLine($"  +0x10 StructureType        {blob.Header.RleType} ({(int)blob.Header.RleType})");
        builder.AppendLine($"  +0x14 BookmarkCount        {blob.Header.BookmarkCount}");
        builder.AppendLine($"  +0x18 BookmarkDistance     {blob.Header.BookmarkDistance}");
        builder.AppendLine($"  +0x1C RleArrayCount        {blob.Header.RleArrayCount}");
        builder.AppendLine($"  +0x20 RleEntrySize         {blob.Header.RleEntrySize}");
        builder.AppendLine($"  +0x22 BitpackEntrySize     {blob.Header.BitpackEntrySize} bits, "
                           + $"{blob.Bitpack.ValuesPerUnit} per 64 bit unit");
        builder.AppendLine($"  +0x24 BitpackUnitCount     {blob.Header.BitpackUnitCount:N0}");
        builder.AppendLine($"  +0x28 BitpackMinId         {blob.Header.BitpackMinId}");

        builder.AppendLine();
        builder.AppendLine($"Layout   header 0..48   bookmarks {blob.Header.BookmarkArrayOffset:N0}..{blob.Header.RleArrayOffset:N0}   "
                           + $"rle {blob.Header.RleArrayOffset:N0}..{blob.Header.BitpackArrayOffset:N0}   "
                           + $"bitpack {blob.Header.BitpackArrayOffset:N0}..{blob.Header.ExpectedSize:N0}");

        builder.AppendLine($"Rows     {blob.RowCount:N0} total, {blob.BitpackRowCount:N0} bit packed, "
                           + $"{blob.LiteralRunCount:N0} literal runs");

        builder.AppendLine();
        builder.AppendLine($"RLE Array ({blob.Header.RleEntryCount} entries of {blob.Header.RleEntryBytes} bytes)");

        AppendSample(builder, blob.Header.RleEntryCount, i => FormatRleEntry(blob, i));

        builder.AppendLine();
        builder.AppendLine($"Bookmark Array ({blob.Header.BookmarkCount} entries, every {blob.Header.BookmarkDistance:N0} rows)");

        AppendSample(builder,
                     blob.Header.BookmarkCount,
                     i => $"  [{i,6}] rle entry {blob.Bookmarks[i].GetRleEntryIndex(blob.Header.RleEntryBytes),8}   "
                          + $"end row {blob.Bookmarks[i].EndRow,12:N0}");

        if (blob.Header.BitpackUnitCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"Bit Pack Array ({blob.Bitpack.Count:N0} values at {blob.Header.BitpackEntrySize} bits)");

            AppendSample(builder, blob.Bitpack.Count, i => FormatBitpackValue(blob, i));
        }

        return builder.ToString();
    }

    public static string DumpDictionary(SegmentDictionary metadata, DictionaryBlob blob)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Dictionary {metadata.DictionaryId} ({(metadata.IsGlobal ? "global" : "local")})   "
                           + $"column {metadata.ColumnId}");

        builder.AppendLine($"  Version {blob.Version}   LobType {blob.LobType}   entries {blob.EntryCount:N0}   "
                           + $"ids {blob.FirstId}..{blob.FirstId + blob.EntryCount - 1}   "
                           + $"size {metadata.OnDiskSize:N0}");

        builder.AppendLine();

        switch (blob)
        {
            case NumericDictionary numeric:
                builder.AppendLine($"  Numeric   elementSize {numeric.ElementSize}   bucketSize {numeric.HashTable.BucketSize}   "
                                   + $"bucketCount {numeric.HashTable.BucketCount}   collisions {numeric.HashTable.CollisionCount}");

                builder.AppendLine();

                AppendSample(builder,
                             numeric.Values.Length,
                             i => $"  [{i,6}] id {blob.FirstId + i,8}   value {numeric.Values[i],22}");
                break;

            case StringDictionary strings:
                builder.AppendLine($"  String    maxStringSize {strings.MaxStringSize:N0}   "
                                   + $"handleSize {strings.HandleSize}   pages {strings.Pages.Length}");

                foreach (var page in strings.Pages)
                {
                    builder.AppendLine($"  Page      {page.SubLobType}   offset {page.Offset:N0}   "
                                       + $"size {page.Size:N0}   strings {page.StringCount:N0}"
                                       + FormatHuffmanPage(page));
                }

                builder.AppendLine();

                AppendSample(builder,
                             strings.Handles.Length,
                             i => $"  [{i,6}] id {blob.FirstId + i,8}   page {strings.Handles[i].Page}   "
                                  + $"offset {strings.Handles[i].Offset,10:N0}   value \"{strings.GetValueAt(i)}\"");
                break;
        }

        return builder.ToString();
    }

    public static string DumpDecodedRows(ColumnSegment segment, SegmentReader reader, int count)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Decoded rows for column {segment.Key.ColumnId} ({segment.Column?.Name}), "
                           + $"first {Math.Min(count, reader.RowCount)} of {reader.RowCount:N0}");

        builder.AppendLine();

        for (var i = 0; i < Math.Min(count, reader.RowCount); i++)
        {
            builder.AppendLine($"  [{i,6}] dataId {reader.DataIds.GetDataId(i),22}   value {reader.GetValue(i)}");
        }

        return builder.ToString();
    }

    private static string FormatRleEntry(SegmentBlob blob, int index)
    {
        var entry = blob.RleEntries[index];

        if (entry.IsTerminator)
        {
            return $"  [{index,6}] terminator";
        }

        return !entry.IsValue
            ? $"  [{index,6}] bitpack from {entry.BitpackIndex,12:N0}   count {entry.Count,10:N0}"
            : $"  [{index,6}] value        {entry.Value,12}   count {entry.Count,10:N0}";
    }

    private static string FormatBitpackValue(SegmentBlob blob, int index)
    {
        var span = blob.Bitpack.GetSpan(index);

        return $"  [{index,6}] {blob.Bitpack[index],22}   bit {span.BitOffset,12:N0} len {span.BitLength,3}   "
               + $"bytes {blob.Header.BitpackArrayOffset + span.ByteOffset,10:N0}+{span.ByteLength}";
    }

    private static string FormatHuffmanPage(StringPage page)
        => page is HuffmanStringPage huffman
            ? $"   huffmanType {huffman.HuffmanBlobType}   decoderBits {huffman.DecoderBitSize}   "
              + $"bitCount {huffman.BitCount:N0}   compressed {huffman.CompressedDataSize:N0}"
            : string.Empty;

    private static void AppendSample(StringBuilder builder, int count, Func<int, string> format)
    {
        if (count <= SampleSize * 2)
        {
            for (var i = 0; i < count; i++)
            {
                builder.AppendLine(format(i));
            }

            return;
        }

        for (var i = 0; i < SampleSize; i++)
        {
            builder.AppendLine(format(i));
        }

        builder.AppendLine($"     ...  ({count - (SampleSize * 2):N0} more)");

        for (var i = count - SampleSize; i < count; i++)
        {
            builder.AppendLine(format(i));
        }
    }

    private static string Truncate(string? value, int length)
        => value is null ? string.Empty : value.Length <= length ? value : value[..length];
}
