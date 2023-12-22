using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface IStructureInfoProvider
{
    Task<IndexStructure> GetIndexStructure(long allocationUnitId);

    Task<CompressionType> GetCompressionType(long partitionId);
}