using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;

public interface ICompressedDataRecordService
{
    CompressedDataRecord Load(Page page, ushort slotOffset, Structure structure);
}
