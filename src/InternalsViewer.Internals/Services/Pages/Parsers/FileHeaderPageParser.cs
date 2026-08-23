using System.Buffers.Binary;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Services.Pages.Parsers;

/// <summary>
/// Parser for the File Header page
/// </summary>
/// <remarks>
/// The header is held in a single record with one variable length column per property, so a property is located by its column id
/// rather than a fixed offset
/// </remarks>
public sealed class FileHeaderPageParser : PageParser, IPageParser<FileHeaderPage>
{
    private const int GuidSize = 16;

    private const int FixedLengthSizeOffset = 2;

    private const int BindingIdColumn = 1;
    private const int FileIdColumn = 3;
    private const int FileGroupIdColumn = 4;
    private const int FileSizeColumn = 5;
    private const int MaxSizeColumn = 6;
    private const int GrowthColumn = 7;
    private const int PerfColumn = 8;
    private const int BackupLsnColumn = 9;
    private const int FirstUpdateLsnColumn = 10;
    private const int OldestRestoredLsnColumn = 11;
    private const int MinSizeColumn = 13;
    private const int StatusColumn = 14;
    private const int UserShrinkSizeColumn = 15;
    private const int SectorSizeColumn = 16;
    private const int MaxLsnColumn = 17;
    private const int FirstLsnColumn = 19;
    private const int CreateLsnColumn = 20;
    private const int DifferentialBaseLsnColumn = 21;
    private const int DifferentialBaseGuidColumn = 22;
    private const int FileOfflineLsnColumn = 23;
    private const int FileIdGuidColumn = 24;
    private const int RestoreStatusColumn = 25;
    private const int RestoreRedoStartLsnColumn = 26;
    private const int LogicalNameColumn = 28;

    public PageType[] SupportedPageTypes => [PageType.FileHeader];

    Page IPageParser.Parse(PageData page)
    {
        return Parse(page);
    }

    public FileHeaderPage Parse(PageData page)
    {
        var fileHeaderPage = CopyToPageType<FileHeaderPage>(page);

        var columns = ParseColumns(fileHeaderPage);

        ReadValues(fileHeaderPage, columns);

        SetMarkers(fileHeaderPage, columns);

        return fileHeaderPage;
    }
    
    /// <summary>
    /// Walks the record's variable length column offset array to give the position of every column
    /// </summary>
    private static Column[] ParseColumns(FileHeaderPage page)
    {
        if (page.OffsetTable.Length == 0)
        {
            return [];
        }

        var recordOffset = page.OffsetTable[0];

        if (recordOffset is 0 or >= PageData.Size)
        {
            return [];
        }

        var data = page.Data.AsSpan();

        var fixedLengthSize = BinaryPrimitives.ReadUInt16LittleEndian(data[(recordOffset + FixedLengthSizeOffset)..]);

        var columnCountOffset = recordOffset + fixedLengthSize;

        if (columnCountOffset + sizeof(ushort) > PageData.Size)
        {
            return [];
        }

        var columnCount = BinaryPrimitives.ReadUInt16LittleEndian(data[columnCountOffset..]);

        var nullBitmapSize = (columnCount + 7) / 8;

        var variableColumnCountOffset = columnCountOffset + sizeof(ushort) + nullBitmapSize;

        if (variableColumnCountOffset + sizeof(ushort) > PageData.Size)
        {
            return [];
        }

        var variableColumnCount = BinaryPrimitives.ReadUInt16LittleEndian(data[variableColumnCountOffset..]);

        var offsetArrayOffset = variableColumnCountOffset + sizeof(ushort);

        var start = offsetArrayOffset + (variableColumnCount * sizeof(ushort));

        if (start > PageData.Size)
        {
            return [];
        }

        var columns = new Column[variableColumnCount];

        for (var index = 0; index < variableColumnCount; index++)
        {
            var end = recordOffset + BinaryPrimitives.ReadUInt16LittleEndian(data[(offsetArrayOffset + (index * sizeof(ushort)))..]);

            end = Math.Clamp(end, start, PageData.Size);

            columns[index] = new Column(start, end - start);

            start = end;
        }

        return columns;
    }

    private static void ReadValues(FileHeaderPage page, Column[] columns)
    {
        page.LogicalName = ReadString(page.Data, columns, LogicalNameColumn);

        page.BindingId = ReadGuid(page.Data, columns, BindingIdColumn);

        page.FileIdGuid = ReadGuid(page.Data, columns, FileIdGuidColumn);

        page.DifferentialBaseGuid = ReadGuid(page.Data, columns, DifferentialBaseGuidColumn);

        page.FileId = ReadInt16(page.Data, columns, FileIdColumn);

        page.FileGroupId = ReadInt16(page.Data, columns, FileGroupIdColumn);

        page.FileSize = ReadInt32(page.Data, columns, FileSizeColumn);

        page.MaxSize = ReadInt32(page.Data, columns, MaxSizeColumn);

        page.MinSize = ReadInt32(page.Data, columns, MinSizeColumn);

        page.UserShrinkSize = ReadInt32(page.Data, columns, UserShrinkSizeColumn);

        page.Growth = ReadInt32(page.Data, columns, GrowthColumn);

        page.Perf = ReadInt32(page.Data, columns, PerfColumn);

        page.Status = ReadInt32(page.Data, columns, StatusColumn);

        page.SectorSize = ReadInt32(page.Data, columns, SectorSizeColumn);

        page.RestoreStatus = ReadInt32(page.Data, columns, RestoreStatusColumn);

        page.BackupLsn = ReadLogSequenceNumber(page.Data, columns, BackupLsnColumn);

        page.FirstUpdateLsn = ReadLogSequenceNumber(page.Data, columns, FirstUpdateLsnColumn);

        page.OldestRestoredLsn = ReadLogSequenceNumber(page.Data, columns, OldestRestoredLsnColumn);

        page.MaxLsn = ReadLogSequenceNumber(page.Data, columns, MaxLsnColumn);

        page.FirstLsn = ReadLogSequenceNumber(page.Data, columns, FirstLsnColumn);

        page.CreateLsn = ReadLogSequenceNumber(page.Data, columns, CreateLsnColumn);

        page.DifferentialBaseLsn = ReadLogSequenceNumber(page.Data, columns, DifferentialBaseLsnColumn);

        page.FileOfflineLsn = ReadLogSequenceNumber(page.Data, columns, FileOfflineLsnColumn);

        page.RestoreRedoStartLsn = ReadLogSequenceNumber(page.Data, columns, RestoreRedoStartLsnColumn);
    }

    private static void SetMarkers(FileHeaderPage page, Column[] columns)
    {
        MarkColumn(page, columns, nameof(FileHeaderPage.BindingId), BindingIdColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.FileId), FileIdColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.FileGroupId), FileGroupIdColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.FileSize), FileSizeColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.MaxSize), MaxSizeColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.Growth), GrowthColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.Perf), PerfColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.BackupLsn), BackupLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.FirstUpdateLsn), FirstUpdateLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.OldestRestoredLsn), OldestRestoredLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.MinSize), MinSizeColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.Status), StatusColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.UserShrinkSize), UserShrinkSizeColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.SectorSize), SectorSizeColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.MaxLsn), MaxLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.FirstLsn), FirstLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.CreateLsn), CreateLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.DifferentialBaseLsn), DifferentialBaseLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.DifferentialBaseGuid), DifferentialBaseGuidColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.FileOfflineLsn), FileOfflineLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.FileIdGuid), FileIdGuidColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.RestoreStatus), RestoreStatusColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.RestoreRedoStartLsn), RestoreRedoStartLsnColumn);
        MarkColumn(page, columns, nameof(FileHeaderPage.LogicalName), LogicalNameColumn);
    }

    private static void MarkColumn(FileHeaderPage page, Column[] columns, string propertyName, int columnId)
    {
        if (TryGetColumn(columns, columnId, 1, out var column))
        {
            page.MarkProperty(propertyName, column.Offset, column.Length);
        }
    }

    private static bool TryGetColumn(Column[] columns, int columnId, int minimumLength, out Column column)
    {
        column = default;

        if (columnId > columns.Length)
        {
            return false;
        }

        var candidate = columns[columnId - 1];

        if (candidate.Length < minimumLength)
        {
            return false;
        }

        column = candidate;

        return true;
    }

    private static short ReadInt16(byte[] data, Column[] columns, int columnId)
    {
        return TryGetColumn(columns, columnId, sizeof(short), out var column)
               ? BitConverter.ToInt16(data, column.Offset)
               : default;
    }

    private static int ReadInt32(byte[] data, Column[] columns, int columnId)
    {
        return TryGetColumn(columns, columnId, sizeof(int), out var column)
               ? BitConverter.ToInt32(data, column.Offset)
               : 0;
    }

    private static Guid ReadGuid(byte[] data, Column[] columns, int columnId)
    {
        return TryGetColumn(columns, columnId, GuidSize, out var column)
               ? new Guid(data.AsSpan(column.Offset, GuidSize))
               : Guid.Empty;
    }

    private static LogSequenceNumber ReadLogSequenceNumber(byte[] data, Column[] columns, int columnId)
    {
        return TryGetColumn(columns, columnId, LogSequenceNumber.Size, out var column)
            ? LogSequenceNumberParser.Parse(data.AsSpan(column.Offset, LogSequenceNumber.Size))
            : default;
    }

    private static string ReadString(byte[] data, Column[] columns, int columnId)
    {
        return TryGetColumn(columns, columnId, sizeof(char), out var column)
            ? Encoding.Unicode.GetString(data, column.Offset, column.Length)
            : string.Empty;
    }

    /// <summary>
    /// Position and length of a variable length column, relative to the start of the page
    /// </summary>
    private readonly record struct Column(int Offset, int Length);
}
