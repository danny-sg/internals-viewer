using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Services.Loaders.Compression;

public class CompressedDataRecordService: ICompressedDataRecordService
{
    public CompressedDataRecord Load(Page page, ushort slotOffset, Structure structure)
    {
        throw new System.NotImplementedException();
    }
}