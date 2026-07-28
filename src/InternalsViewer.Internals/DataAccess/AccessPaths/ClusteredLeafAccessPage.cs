using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.DataAccess;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.DataAccess.AccessPaths;

/// <summary>
/// Adapts a decoded clustered index leaf <see cref="DataPage"/> and its records to the seek executor's
/// page contract
/// </summary>
/// <remarks>
/// A clustered index's leaf level is the table's data pages rather than index pages, but the key
/// columns are still described by the clustered index's <see cref="IndexStructure"/>, so this adapter
/// mirrors <see cref="IndexAccessPage"/> while treating the page as always being the leaf.
/// </remarks>
public sealed class ClusteredLeafAccessPage(DataPage page, List<IRecord> records, IndexStructure indexStructure)
    : IIndexAccessPage
{
    public PageAddress PageAddress => page.PageHeader.PageAddress;

    public PageAddress NextPage => page.PageHeader.NextPage;

    public byte Level => 0;

    public bool IsRoot => page.AllocationUnit.RootPage == PageAddress;

    public bool IsLeaf => true;

    public int SlotCount => records.Count;

    public IRecord GetRecord(int slot) => records[slot];

    public AccessKey GetKey(int slot)
    {
        var record = records[slot];

        var values = indexStructure.IndexKeyColumns
                                    .Select(keyColumn => CreateValue(record, keyColumn))
                                    .ToArray();

        return AccessKey.Create(values);
    }

    public int CompareKeyPrefix(int slot, in AccessKey target, int width)
    {
        var key = GetKey(slot);

        return key.ComparePrefix(target, width);
    }

    public PageAddress GetChildPage(int slot)
    {
        throw new NotSupportedException("The page is a leaf.");
    }

    private static AccessValue CreateValue(IRecord record, IndexColumnStructure keyColumn)
    {
        var field = record.Fields.FirstOrDefault(f => f.ColumnStructure.ColumnId == keyColumn.ColumnId);

        return field is null
            ? AccessValue.FromNull(keyColumn.DataType).WithColumnName(keyColumn.ColumnName)
            : AccessValueFieldFactory.Create(field);
    }
}
