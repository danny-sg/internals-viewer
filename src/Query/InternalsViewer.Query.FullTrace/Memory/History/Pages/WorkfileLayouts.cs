using System.Collections.Concurrent;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Memory.History.Pages;

public sealed class WorkfileLayouts(DatabaseSource database, Func<ulong, PlanNode?> getPlanNode)
{
    private ConcurrentDictionary<ulong, RowRecordLayout?> Layouts { get; } = new();

    private ConcurrentDictionary<ulong, SortRecordLayout?> SortLayouts { get; } = new();

    private ConcurrentDictionary<string, Dictionary<string, ColumnStructure>?> Tables { get; } = new();

    public RowRecordLayout? Get(ulong iterator) => iterator == 0 ? null : Layouts.GetOrAdd(iterator, Create);

    public SortRecordLayout? GetSort(ulong iterator) => iterator == 0 ? null : SortLayouts.GetOrAdd(iterator, CreateSort);

    private RowRecordLayout? Create(ulong iterator)
    {
        if (getPlanNode(iterator) is not { HashInfo: not null } hashMatch)
        {
            return null;
        }

        var columns = HashTableColumns.Get(hashMatch);

        return RowRecordLayout.Create(columns.Count, HashTableColumns.GetKeyCount(hashMatch), o => GetStructure(columns[o]));
    }

    private SortRecordLayout? CreateSort(ulong iterator)
    {
        if (getPlanNode(iterator) is not { SortColumns.Count: > 0 } sort || sort.Children.FirstOrDefault() is not { } input)
        {
            return null;
        }

        var columns = input.OutputColumns;

        return SortRecordLayout.Create(columns.Count, o => GetStructure(columns[o]), o => IsSortKey(sort, columns[o]));
    }

    private static bool IsSortKey(PlanNode sort, ColumnReference column)
        => sort.SortColumns.Any(k => k.Column.TableKey == column.TableKey
                                     && string.Equals(k.Column.Column, column.Column, StringComparison.OrdinalIgnoreCase));

    private ColumnStructure? GetStructure(ColumnReference column)
    {
        if (column.Table.Length == 0)
        {
            return null;
        }

        var columns = Tables.GetOrAdd(column.TableKey, _ => LoadTable(column.Schema, column.Table));

        return columns?.GetValueOrDefault(column.Column);
    }

    private Dictionary<string, ColumnStructure>? LoadTable(string schema, string table)
    {
        if (AllocationUnitProvider.FindTableAllocationUnit(database, schema, table) is not { } allocationUnit)
        {
            return null;
        }

        return TableStructureProvider.GetTableStructure(database, allocationUnit.AllocationUnitId)
                                     .Columns
                                     .Where(c => !c.IsDropped)
                                     .GroupBy(c => c.ColumnName, StringComparer.OrdinalIgnoreCase)
                                     .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }
}
