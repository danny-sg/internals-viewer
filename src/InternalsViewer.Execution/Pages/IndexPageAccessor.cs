using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces.Pages;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Execution.Pages;

/// <summary>
/// Adapts a decoded <see cref="IndexPage"/> and its records to the seek executor page contract
/// </summary>
public sealed class IndexPageAccessor(IndexPage page, IRecordService recordService, IndexStructure indexStructure)
    : IIndexPageAccessor
{
    private readonly IIndexRecord?[] _records = new IIndexRecord?[page.OffsetTable.Length];

    public PageAddress PageAddress => page.PageHeader.PageAddress;

    public PageAddress NextPage => page.PageHeader.NextPage;

    public PageAddress PreviousPage => page.PageHeader.PreviousPage;

    public byte Level => page.PageHeader.Level;

    public bool IsLeaf => Level == 0;

    public bool IsRoot => page.AllocationUnit.RootPage == PageAddress;

    public int SlotCount => _records.Length;

    public IRecord GetRecord(int slot) => GetIndexRecord(slot);

    public AccessKey GetKey(int slot) => RecordKeyReader.GetKey(GetIndexRecord(slot), indexStructure);

    public int CompareKeyPrefix(int slot, in AccessKey target, int width)
    {
        var key = GetKey(slot);

        return RecordKeyReader.ComparePrefix(key, target, width, indexStructure);
    }

    public PageAddress GetChildPage(int slot)
    {
        if (IsLeaf)
        {
            throw new NotSupportedException("The page is a leaf");
        }

        return GetIndexRecord(slot).DownPagePointer;
    }

    private IIndexRecord GetIndexRecord(int slot) 
        => _records[slot] ??= recordService.GetIndexRecord(page, slot, indexStructure);
}
