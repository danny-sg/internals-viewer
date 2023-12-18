using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Services.Records.Loaders;

namespace InternalsViewer.Internals.Services.Records;

public class RecordService(IStructureInfoProvider structureInfoProvider) : IRecordService
{
    public IStructureInfoProvider StructureInfoProvider { get; } = structureInfoProvider;

    public async Task<DataRecord> GetDataRecord(Page page, ushort offset)
    {
        var structure = await StructureInfoProvider.GetTableStructure(page.PageHeader.AllocationUnitId);

        return DataRecordLoader.Load(page, offset, structure);
    }

    public async Task<IndexRecord> GetIndexRecord(Page page, ushort offset)
    {
        var structure = await StructureInfoProvider.GetIndexStructure(page.PageHeader.AllocationUnitId);

        return IndexRecordLoader.Load(page, offset, structure);
    }
}