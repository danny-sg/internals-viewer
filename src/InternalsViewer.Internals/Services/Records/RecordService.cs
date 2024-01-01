using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Loaders.Records;

namespace InternalsViewer.Internals.Services.Records;

public class RecordService(IndexRecordLoader indexRecordLoader, DataRecordLoader dataRecordLoader) : IRecordService
{
    public IndexRecordLoader IndexRecordLoader { get; } = indexRecordLoader;

    public DataRecordLoader DataRecordLoader { get; } = dataRecordLoader;

    public DataRecord GetDataRecord(DataPage page, ushort offset)
    {
        var structure = TableStructureProvider.GetTableStructure(page.Database.Metadata, 
                                                                 page.PageHeader.AllocationUnitId);

        return DataRecordLoader.Load(page, offset, structure);
    }

    public IndexRecord GetIndexRecord(IndexPage page, ushort offset)
    {
        var structure = IndexStructureProvider.GetIndexStructure(page.Database.Metadata,
                                                                 page.PageHeader.AllocationUnitId);

        return IndexRecordLoader.Load(page, offset, structure);
    }
}