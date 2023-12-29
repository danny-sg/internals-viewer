using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Services.Loaders.Compression;

public class CompressedDataRecordService: ICompressedDataRecordService
{
    public CompressedDataRecord Load(AllocationUnitPage page, ushort slotOffset, TableStructure structure)
    {
        throw new System.NotImplementedException();
    }
}