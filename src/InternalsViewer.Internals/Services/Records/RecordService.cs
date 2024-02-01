using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Loaders.Records;

namespace InternalsViewer.Internals.Services.Records;

public class RecordService(FixedVarIndexRecordLoader fixedVarIndexRecordLoader, FixedVarRecord fixedVarRecord, CdDataRecordLoader cdDataRecordLoader) : IRecordService
{
    private FixedVarIndexRecordLoader FixedVarIndexRecordLoader { get; } = fixedVarIndexRecordLoader;

    private FixedVarRecord FixedVarRecord { get; } = fixedVarRecord;

    private CdDataRecordLoader CdDataRecordLoader { get; } = cdDataRecordLoader;

    public List<DataRecord> GetDataRecords(DataPage page)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select(s => FixedVarRecord.Load(page, s, structure)).ToList();
    }

    public List<CompressedDataRecord> GetCompressedDataRecords(DataPage page)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select(s => CdDataRecordLoader.Load(page, s, structure)).ToList();
    }

    public List<IndexRecord> GetIndexRecords(IndexPage page)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
            page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select(s => FixedVarIndexRecordLoader.Load(page, s, structure)).ToList();
    }

    public DataRecord GetDataRecord(DataPage page, ushort offset)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return FixedVarRecord.Load(page, offset, structure);
    }

    public IndexRecord GetIndexRecord(IndexPage page, ushort offset)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return FixedVarIndexRecordLoader.Load(page, offset, structure);
    }
}