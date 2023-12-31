using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Compressed;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Interfaces.Services.Loaders.Compression;

public interface ICompressedDataRecordService
{
    CompressedDataRecord Load(AllocationUnitPage page, ushort slotOffset, TableStructure structure);
}
