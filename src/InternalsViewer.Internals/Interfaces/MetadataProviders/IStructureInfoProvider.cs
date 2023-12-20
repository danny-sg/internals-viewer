using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Interfaces.MetadataProviders;

public interface IStructureInfoProvider
{
    Task<TableStructure> GetTableStructure(long allocationUnitId);

    Task<IndexStructure> GetIndexStructure(long allocationUnitId);

    Task<StructureType> GetStructureType(string name);

    Task<List<HobtEntryPoint>> GetEntryPoints(string objectName, string indexName);

    Task<CompressionType> GetCompressionType(long partitionId);

    Task<string?> GetName(long allocationUnitId);
}