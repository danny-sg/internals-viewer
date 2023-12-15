using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Interfaces.MetadataProviders;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Services.Records.Loaders;

namespace InternalsViewer.Internals.Services.Records;

internal class DataRecordService(IStructureInfoProvider structureInfoProvider)
{
    public IStructureInfoProvider StructureInfoProvider { get; } = structureInfoProvider;

    public async Task<DataRecord> GetDataRecord(Page page, ushort offset)
    {
        var structure = await StructureInfoProvider.GetTableStructure(page.Header.AllocationUnitId);

        return DataRecordLoader.Load(page, offset, structure);
    }
}
