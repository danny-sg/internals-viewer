using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface IStructureInfoProvider
{
    Task<TableStructure> GetTableStructure(long allocationUnitId);

    Task<IndexStructure> GetIndexStructure(long allocationUnitId);

    Task<CompressionType> GetCompressionType(long partitionId);
}