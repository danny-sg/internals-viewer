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
        builder.AppendLine($"  +0x00 Version              {blob.Version}");
        builder.AppendLine($"  +0x04 LobType              {blob.LobType} ({(int)blob.LobType})");
        builder.AppendLine($"  +0x08 Reserved             {blob.Reserved}");
        builder.AppendLine($"  +0x0C Unknown              {blob.Unknown0C} (0x{blob.Unknown0C:X})");
        builder.AppendLine($"  +0x10 StructureType        {blob.StructureType} ({(int)blob.StructureType})");
        builder.AppendLine($"  +0x14 BookmarkCount        {blob.BookmarkCount}");
        builder.AppendLine($"  +0x18 BookmarkDistance     {blob.BookmarkDistance}");
        builder.AppendLine($"  +0x1C RleArrayCount        {blob.RleArrayCount}");
        builder.AppendLine($"  +0x20 RleEntrySize         {blob.RleEntrySize}");
        builder.AppendLine($"  +0x22 BitpackEntrySize     {blob.BitpackEntrySize} bits, "
                           + $"{blob.Bitpack.ValuesPerUnit} per 64 bit unit");
        builder.AppendLine($"  +0x24 BitpackUnitCount     {blob.BitpackUnitCount:N0}");
        builder.AppendLine($"  +0x28 BitpackMinId         {blob.BitpackMinId}");

        builder.AppendLine();
        builder.AppendLine($"Layout   header 0..48   bookmarks {blob.BookmarkArrayOffset:N0}..{blob.RleArrayOffset:N0}   "
                           + $"rle {blob.RleArrayOffset:N0}..{blob.BitpackArrayOffset:N0}   "
                           + $"bitpack {blob.BitpackArrayOffset:N0}..{blob.ExpectedSize:N0}");

        builder.AppendLine($"Rows     {blob.RowCount:N0} total, {blob.BitpackRowCount:N0} bit packed, "
                           + $"{blob.LiteralRunCount:N0} literal runs");

        builder.AppendLine();
        builder.AppendLine($"RLE Array ({blob.RleEntryCount} entries of {blob.RleEntryBytes} bytes)");

        AppendSample(builder, blob.RleEntryCount, i => FormatRleEntry(blob, i));

        builder.AppendLine();
        builder.AppendLine($"Bookmark Array ({blob.BookmarkCount} entries, every {blob.BookmarkDistance:N0} rows)");

        AppendSample(builder,
                     blob.BookmarkCount,
                     i => $"  [{i,6}] rle entry {blob.Bookmarks[i].GetRleEntryIndex(blob.RleEntryBytes),8}   "
                          + $"end row {blob.Bookmarks[i].EndRow,12:N0}");

        if (blob.BitpackUnitCount > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"Bit Pack Array ({blob.Bitpack.Count:N0} values at {blob.BitpackEntrySize} bits)");

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
                builder.AppendLine($"  Numeric   elementSize {numeric.ElementSize}   bucketSize {numeric.BucketSize}   "
                                   + $"bucketCount {numeric.BucketCount}   collisions {numeric.CollisionCount}");

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

        return entry.IsBitpacked
            ? $"  [{index,6}] bitpack from {entry.BitpackIndex,12:N0}   count {entry.Count,10:N0}"
            : $"  [{index,6}] value        {entry.Value,12}   count {entry.Count,10:N0}";
    }

    private static string FormatBitpackValue(SegmentBlob blob, int index)
    {
        var span = blob.Bitpack.GetSpan(index);

        return $"  [{index,6}] {blob.Bitpack[index],22}   bit {span.BitOffset,12:N0} len {span.BitLength,3}   "
               + $"bytes {blob.BitpackArrayOffset + span.ByteOffset,10:N0}+{span.ByteLength}";
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
