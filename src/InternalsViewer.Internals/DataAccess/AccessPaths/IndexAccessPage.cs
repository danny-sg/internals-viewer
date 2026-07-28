using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.DataAccess;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.DataAccess.AccessPaths;

/// <summary>
/// Adapts a decoded <see cref="IndexPage"/> and its records to the seek executor's page contract
/// </summary>
/// <remarks>
/// The executor operates purely on <see cref="IIndexAccessPage"/>/<see cref="IAccessPage"/>, so this
/// adapter is the only place that understands how key columns map onto decoded record fields.
/// </remarks>
public sealed class IndexAccessPage(IndexPage page, List<IIndexRecord> records, IndexStructure indexStructure)
    : IIndexAccessPage
{
    public PageAddress PageAddress => page.PageHeader.PageAddress;

    public PageAddress NextPage => page.PageHeader.NextPage;

    public byte Level => page.PageHeader.Level;

    public bool IsLeaf => Level == 0;

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
        if (IsLeaf)
        {
            throw new NotSupportedException("The page is a leaf.");
        }

        return records[slot].DownPagePointer;
    }

    private static AccessValue CreateValue(IIndexRecord record, IndexColumnStructure keyColumn)
    {
        var field = record.Fields.FirstOrDefault(f => f.ColumnStructure.ColumnId == keyColumn.ColumnId);

        return field is null
            ? AccessValue.FromNull(keyColumn.DataType).WithColumnName(keyColumn.ColumnName)
            : AccessValueFieldFactory.Create(field);
    }
}