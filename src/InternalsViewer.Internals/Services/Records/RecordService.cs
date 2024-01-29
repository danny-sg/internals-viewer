using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Loaders.Records;

namespace InternalsViewer.Internals.Services.Records;

public class RecordService(IndexFixedVarRecordLoader indexFixedVarRecordLoader, DataFixedVarRecordLoader dataFixedVarRecordLoader, CompressedDataRecordLoader compressedDataRecordLoader) : IRecordService
{
    private IndexFixedVarRecordLoader IndexFixedVarRecordLoader { get; } = indexFixedVarRecordLoader;

    private DataFixedVarRecordLoader DataFixedVarRecordLoader { get; } = dataFixedVarRecordLoader;

    private CompressedDataRecordLoader CompressedDataRecordLoader { get; } = compressedDataRecordLoader;

    public List<DataRecord> GetDataRecords(DataPage page)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select(s => DataFixedVarRecordLoader.Load(page, s, structure)).ToList();
    }

    public List<CompressedDataRecord> GetCompressedDataRecords(DataPage page)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select(s => CompressedDataRecordLoader.Load(page, s, structure)).ToList();
    }

    public List<IndexRecord> GetIndexRecords(IndexPage page)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
            page.PageHeader.AllocationUnitId);

        return page.OffsetTable.Select(s => IndexFixedVarRecordLoader.Load(page, s, structure)).ToList();
    }

    public DataRecord GetDataRecord(DataPage page, ushort offset)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return DataFixedVarRecordLoader.Load(page, offset, structure);
    }

    public IndexRecord GetIndexRecord(IndexPage page, ushort offset)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return IndexFixedVarRecordLoader.Load(page, offset, structure);
    }
}