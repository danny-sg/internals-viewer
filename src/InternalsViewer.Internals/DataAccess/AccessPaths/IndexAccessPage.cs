using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.DataAccess;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.DataAccess.AccessPaths;

/// <summary>
/// Adapts a decoded <see cref="IndexPage"/> and its records to the seek executor's page contract
/// </summary>
/// <remarks>
/// The executor operates purely on <see cref="IIndexAccessPage"/>/<see cref="IAccessPage"/>, so this
/// adapter is the only place that understands how key columns map onto decoded record fields.
/// </remarks>
public sealed class IndexAccessPage(IndexPage page, IRecordService recordService, IndexStructure indexStructure)
    : IIndexAccessPage
{
    private readonly IIndexRecord?[] _records = new IIndexRecord?[page.OffsetTable.Length];

    public PageAddress PageAddress => page.PageHeader.PageAddress;

    public PageAddress NextPage => page.PageHeader.NextPage;

    public byte Level => page.PageHeader.Level;

    public bool IsLeaf => Level == 0;

    public bool IsRoot => page.AllocationUnit.RootPage == PageAddress;

    public int SlotCount => _records.Length;

    public IRecord GetRecord(int slot) => GetIndexRecord(slot);

    private IIndexRecord GetIndexRecord(int slot) => _records[slot] ??= recordService.GetIndexRecord(page, slot, indexStructure);

    public AccessKey GetKey(int slot)
    {
        var record = GetIndexRecord(slot);

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

        return GetIndexRecord(slot).DownPagePointer;
    }

    private static AccessValue CreateValue(IIndexRecord record, IndexColumnStructure keyColumn)
    {
        var field = record.Fields.FirstOrDefault(f => f.ColumnStructure.ColumnId == keyColumn.ColumnId);

        return field is null
            ? AccessValue.FromNull(keyColumn.DataType).WithColumnName(keyColumn.ColumnName)
            : AccessValueFieldFactory.Create(field);
    }
}