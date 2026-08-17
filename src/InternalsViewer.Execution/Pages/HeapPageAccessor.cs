using InternalsViewer.Execution.Interfaces.Pages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Execution.Pages;

public sealed class HeapPageAccessor(DataPage page, IRecordService recordService) : IRowPageAccessor
{
    private readonly IRecord?[] _records = new IRecord?[page.OffsetTable.Length];

    private readonly TableStructure _tableStructure = TableStructureProvider.GetTableStructure(page.Database,
                                                                                               page.PageHeader.AllocationUnitId);

    public PageAddress PageAddress => page.PageHeader.PageAddress;

    public byte Level => 0;

    public bool IsLeaf => true;

    public int SlotCount => _records.Length;

    public StructureType Structure => page.AllocationUnit.IndexType == IndexType.Heap ? StructureType.Heap : StructureType.BTree;

    public IRecord GetRecord(int slot) 
        => _records[slot] ??= recordService.GetDataRecord(page, slot, _tableStructure);
}
