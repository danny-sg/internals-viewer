using InternalsViewer.Internals.Compression;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using InternalsViewer.Internals.Engine.Parsers;

namespace InternalsViewer.Internals.Providers.Metadata;

/// <summary>
/// Provider responsible for providing allocation unit information from the metadata collection
/// </summary>
public class AllocationUnitProvider
{
    private const int SchemaClassId = 50;

    public static List<AllocationUnit> GetAllocationUnits(InternalMetadata metadata)
    {
        return metadata.AllocationUnits.Select(a => GetAllocationUnit(metadata, a)).ToList();
    }

    private static AllocationUnit GetAllocationUnit(InternalMetadata metadata, InternalAllocationUnit source)
    {
        var rowSet = metadata.RowSets
                             .First(r => r.RowSetId == source.ContainerId);

        var internalObject = metadata.Objects
                                     .First(o => o.ObjectId == rowSet.ObjectId);

        var schema = metadata.Entities
                              .First(s => s.Id == internalObject.SchemaId && s.ClassId == SchemaClassId);

        var index = metadata.Indexes
                            .FirstOrDefault(i => i.ObjectId == rowSet.ObjectId && i.IndexId == rowSet.IndexId);

        var parentIndex = metadata.Indexes
                                  .FirstOrDefault(i => i.ObjectId == internalObject.ParentObjectId 
                                                       && i.IndexId <= 1);

        var displayName = !string.IsNullOrEmpty(index?.Name)
            ? $"{schema.Name}.{internalObject.Name}.{index.Name}"
            : $"{schema.Name}.{internalObject.Name}";

        var allocationUnit = new AllocationUnit
        {
            AllocationUnitId = source.AllocationUnitId,
            ObjectId = rowSet.ObjectId,
            IndexId = rowSet.IndexId,
            SchemaName = schema.Name,
            TableName = internalObject.Name,
            IndexName = index?.Name ?? string.Empty,
            IndexType = index?.IndexType ?? 0,
            IsSystem = (internalObject.Status & 1) != 0,
            PartitionId = source.ContainerId,
            FirstPage = PageAddressParser.Parse(source.FirstPage!),
            RootPage = PageAddressParser.Parse(source.RootPage!),
            FirstIamPage = PageAddressParser.Parse(source.FirstIamPage!),
            UsedPages = source.UsedPages,
            TotalPages = source.TotalPages,
            DisplayName = displayName,
            CompressionType = (CompressionType)rowSet.CompressionLevel,
            ParentIndexType = parentIndex?.IndexType
        };

        return allocationUnit;
    }
}
