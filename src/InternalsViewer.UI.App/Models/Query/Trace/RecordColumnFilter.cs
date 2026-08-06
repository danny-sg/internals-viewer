using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Query.Trace;

/// <summary>
/// Chooses which of a record's columns a trace shows, from the columns its operator states it outputs
/// </summary>
/// <remarks>
/// A record read from a page carries every column the index stores, which is more than the operator hands upwards. A lookup is the
/// exception, because the bookmark it was found by is worth seeing even though the operator does not output it.
/// </remarks>
public sealed class RecordColumnFilter
{
    public static readonly RecordColumnFilter All = new();

    private HashSet<string> Columns { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    private bool IncludesBookmark { get; init; }

    public static RecordColumnFilter For(PlanNode? node, bool includesBookmark = false)
    {
        if (node is null || node.OutputColumns.Count == 0)
        {
            return All;
        }

        var columns = node.OutputColumns
                          .Select(c => c.Column.Trim('[', ']'))
                          .Where(c => c.Length > 0);

        return new RecordColumnFilter
        {
            Columns = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase),
            IncludesBookmark = includesBookmark
        };
    }

    /// <summary>
    /// Filters a record's fields, falling back to all of them when nothing would be left
    /// </summary>
    public IEnumerable<RecordField> Apply(IReadOnlyList<RecordField> fields)
    {
        if (Columns.Count == 0)
        {
            return fields;
        }

        var kept = fields.Where(Includes).ToList();

        return kept.Count > 0 ? kept : fields;
    }

    private bool Includes(RecordField field)
    {
        if (Columns.Contains(field.ColumnStructure.ColumnName))
        {
            return true;
        }

        return IncludesBookmark && IsBookmark(field.ColumnStructure);
    }

    private static bool IsBookmark(ColumnStructure column)
    {
        if (column is IndexColumnStructure indexColumn)
        {
            return indexColumn.IsRowIdentifier || indexColumn.IsIndexKey;
        }

        return column.IsKey || column.IsUniqueifier;
    }
}
