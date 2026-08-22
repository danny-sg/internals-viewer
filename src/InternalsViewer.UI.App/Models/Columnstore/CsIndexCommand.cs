using System;
using InternalsViewer.UI.App.Controls.Columnstore;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// Builds the undocumented CSINDEX command that dumps the structure behind a region of the drawing
/// </summary>
/// <remarks>
/// Every argument has to be a literal, so the database id is written out rather than left as DB_ID(). The column
/// runs one ahead of the id the catalog reports, which is the numbering the columnstore metadata already uses.
/// </remarks>
public static class CsIndexCommand
{
    private const int SegmentKind = 1;

    private const int GlobalDictionaryKind = 2;

    private const int LocalDictionaryKind = 4;

    /// <summary>
    /// Whether the region is one CSINDEX can be asked about, which is a segment or a dictionary
    /// </summary>
    public static bool CanBuild(ColumnstoreRegion region)
        => region.ElementType switch
        {
            ColumnstoreElementType.Segment => region.Segment is not null,
            ColumnstoreElementType.Dictionary => region.Dictionary is not null,
            _ => false
        };

    public static string? Build(ColumnstoreRegion region, short databaseId, long hobtId, int printMode)
    {
        if (region.ElementType == ColumnstoreElementType.Dictionary && region.Dictionary is { } dictionary)
        {
            return Format(databaseId,
                          dictionary.HobtId == 0 ? hobtId : dictionary.HobtId,
                          dictionary.ColumnId,
                          dictionary.IsGlobal ? 0 : dictionary.DictionaryId,
                          dictionary.IsGlobal ? GlobalDictionaryKind : LocalDictionaryKind,
                          printMode);
        }

        if (region.ElementType == ColumnstoreElementType.Segment && region.Segment is { } segment)
        {
            return Format(databaseId, hobtId, segment.ColumnId, segment.RowGroupId, SegmentKind, printMode);
        }

        return null;
    }

    private static string Format(short databaseId, long hobtId, int columnId, int target, int kind, int printMode)
        => $"DBCC TRACEON (3604);{Environment.NewLine}"
           + $"DBCC CSINDEX({databaseId}, {hobtId}, {columnId}, {target}, {kind}, {printMode});";
}
