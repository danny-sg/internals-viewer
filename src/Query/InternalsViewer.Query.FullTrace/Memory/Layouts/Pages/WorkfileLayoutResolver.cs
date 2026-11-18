using System.Collections.Concurrent;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;

public sealed class WorkfileLayoutResolver(DatabaseSource database, Func<ulong, PlanNode?> getPlanNode)
{
    private ConcurrentDictionary<ulong, RowRecordLayout?> Layouts { get; } = new();

    private ConcurrentDictionary<ulong, SortRecordLayout?> SortLayouts { get; } = new();

    private ConcurrentDictionary<string, Dictionary<string, ColumnStructure>?> Tables { get; } = new();

    public RowRecordLayout? Get(ulong iterator) => iterator == 0 ? null : Layouts.GetOrAdd(iterator, Create);

    public SortRecordLayout? GetSort(ulong iterator, Func<IReadOnlyList<ColumnBinding>> getBindings)
        => iterator == 0 ? null : SortLayouts.GetOrAdd(iterator, i => CreateSort(i, getBindings()));

    public RowRecordLayout? Get(ulong iterator, IReadOnlyList<ColumnBinding> bindings, bool isProbeSide)
    {
        if (bindings.Count == 0)
        {
            return null;
        }

        var hashMatch = getPlanNode(iterator) is { HashInfo: not null } node ? node : null;

        List<ColumnReference> names = hashMatch is null
            ? []
            : isProbeSide ? HashTableColumnResolver.GetProbe(hashMatch) : HashTableColumnResolver.Get(hashMatch);

        var isNamed = names.Count == bindings.Count;

        var columns = new List<ColumnStructure>(bindings.Count);

        foreach (var (binding, index) in bindings.OrderBy(b => b.Ordinal).Select((b, i) => (b, i)))
        {
            if (!SqlTypeHelpers.TryGetSqlType(binding.Type, out var type))
            {
                return null;
            }

            columns.Add(new ColumnStructure
            {
                ColumnName = isNamed ? names[index].Column : $"Column {binding.Ordinal}",
                DataType = type,
                DataLength = (short)binding.MaximumLength
            });
        }

        var keyCount = isNamed && !isProbeSide && hashMatch is not null ? HashTableColumnResolver.GetKeyCount(hashMatch) : 0;

        return new RowRecordLayout(columns, keyCount);
    }

    private RowRecordLayout? Create(ulong iterator)
    {
        if (getPlanNode(iterator) is not { HashInfo: not null } hashMatch)
        {
            return null;
        }

        var columns = HashTableColumnResolver.Get(hashMatch);

        return RowRecordLayout.Create(columns.Count, HashTableColumnResolver.GetKeyCount(hashMatch), o => GetStructure(columns[o]));
    }

    private static List<ColumnReference> GetSortRecordColumns(PlanNode sort)
    {
        List<ColumnReference> keys = [.. sort.SortColumns.Select(k => k.Column)];

        var input = sort.Children.FirstOrDefault()?.OutputColumns ?? [];

        return [.. keys, .. input.Where(c => !keys.Exists(k => IsSameColumn(k, c)))];
    }

    private static bool IsSameColumn(ColumnReference left, ColumnReference right)
        => left.TableKey == right.TableKey && string.Equals(left.Column, right.Column, StringComparison.OrdinalIgnoreCase);

    private SortRecordLayout? CreateSort(ulong iterator, IReadOnlyList<ColumnBinding> bindings)
    {
        var sort = getPlanNode(iterator) is { SortColumns.Count: > 0 } node ? node : null;

        var names = sort is null ? [] : GetSortRecordColumns(sort);

        var keyCount = sort?.SortColumns.Count ?? 0;

        if (bindings.Count == 0)
        {
            return names.Count == 0 ? null : SortRecordLayout.Create(names.Count, o => GetStructure(names[o]), o => o < keyCount);
        }

        var isNamed = names.Count == bindings.Count;

        var columns = new List<ColumnStructure>(bindings.Count);

        foreach (var (binding, index) in bindings.OrderBy(b => b.Ordinal).Select((b, i) => (b, i)))
        {
            if (!SqlTypeHelpers.TryGetSqlType(binding.Type, out var type))
            {
                return null;
            }

            columns.Add(new ColumnStructure
            {
                ColumnName = isNamed ? names[index].Column : $"Column {binding.Ordinal}",
                DataType = type,
                DataLength = (short)binding.MaximumLength
            });
        }

        return SortRecordLayout.Create(columns.Count, o => columns[o], o => isNamed && o < keyCount);
    }

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
