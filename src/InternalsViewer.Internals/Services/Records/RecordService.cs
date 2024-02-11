using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Loaders.Records;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

namespace InternalsViewer.Internals.Services.Records;

/// <summary>
/// Service responsible for loading records from pages
/// </summary>
public class RecordService(FixedVarIndexRecordLoader fixedVarIndexRecordLoader, 
                           FixedVarDataRecordLoader fixedVarDataRecordLoader, 
                           CdDataRecordLoader cdDataRecordLoader) : IRecordService
{
    private FixedVarIndexRecordLoader FixedVarIndexRecordLoader { get; } = fixedVarIndexRecordLoader;

    private FixedVarDataRecordLoader FixedVarDataRecordLoader { get; } = fixedVarDataRecordLoader;

    private CdDataRecordLoader CdDataRecordLoader { get; } = cdDataRecordLoader;

    public List<DataRecord> GetDataRecords(DataPage page)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select((s, index) =>
        {
            var record = FixedVarDataRecordLoader.Load(page, s, structure);

            record.Slot = index;

            return record;
        }).ToList();
    }

    public List<CompressedDataRecord> GetCompressedDataRecords(DataPage page)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable
                   .Select((s, index) =>
                    {
                        var record = CdDataRecordLoader.Load(page, s, structure);
                    
                        record.Slot = index;
                    
                        return record;
                    })
                   .ToList();
    }

    public List<IndexRecord> GetIndexRecords(IndexPage page)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable
                   .Select((s, index) => FixedVarIndexRecordLoader.Load(page, s, index, structure))
                    .ToList();
    }

    public DataRecord GetDataRecord(DataPage page, ushort offset)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return FixedVarDataRecordLoader.Load(page, offset, structure);
    }

    public IndexRecord GetIndexRecord(IndexPage page, ushort offset, int slot)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return FixedVarIndexRecordLoader.Load(page, offset, slot, structure);
    }
}