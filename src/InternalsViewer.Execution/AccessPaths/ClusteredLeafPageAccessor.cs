using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.DataAccess;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Internals.DataAccess.AccessPaths;

/// <summary>
/// Adapts a decoded clustered index leaf <see cref="DataPage"/> and its records to the seek executor page contract
/// </summary>
public sealed class ClusteredLeafPageAccessor(DataPage page, IRecordService recordService, IndexStructure indexStructure)
    : IIndexPageAccessor
{
    private readonly IRecord?[] _records = new IRecord?[page.OffsetTable.Length];

    private readonly TableStructure _tableStructure = TableStructureProvider.GetTableStructure(page.Database,
                                                                                               page.PageHeader.AllocationUnitId);

    public PageAddress PageAddress => page.PageHeader.PageAddress;

    public PageAddress NextPage => page.PageHeader.NextPage;

    public PageAddress PreviousPage => page.PageHeader.PreviousPage;

    public byte Level => 0;

    public bool IsRoot => page.AllocationUnit.RootPage == PageAddress;

    public bool IsLeaf => true;

    public int SlotCount => _records.Length;

    public IRecord GetRecord(int slot) => _records[slot] ??= recordService.GetDataRecord(page, slot, _tableStructure);

    public AccessKey GetKey(int slot) => AccessKeyReader.GetKey(GetRecord(slot), indexStructure);

    public int CompareKeyPrefix(int slot, in AccessKey target, int width)
    {
        var key = GetKey(slot);

        return AccessKeyReader.ComparePrefix(key, target, width, indexStructure);
    }

    public PageAddress GetChildPage(int slot)
    {
        throw new NotSupportedException("The page is a leaf.");
    }
}
