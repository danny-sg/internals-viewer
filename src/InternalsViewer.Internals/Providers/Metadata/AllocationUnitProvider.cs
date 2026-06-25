using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Metadata.Internals.Tables;

namespace InternalsViewer.Internals.Providers.Metadata;

/// <summary>
/// Provider responsible for providing allocation unit information from the metadata collection
/// </summary>
public static class AllocationUnitProvider
{
    public static List<AllocationUnit> GetAllocationUnits(InternalMetadata metadata)
    {
        return metadata.AllocationUnits.Values.Select(a => GetAllocationUnit(metadata, a)).ToList();
    }

    public static AllocationUnit GetAllocationUnit(InternalMetadata metadata, InternalAllocationUnit source)
    {
        var rowSet = metadata.RowSets[source.ContainerId];

        var internalObject = metadata.Objects[rowSet.ObjectId];

        var schema = metadata.Entities[(internalObject.SchemaId, (byte)MetadataConstants.SchemaClassId)];

        var index = metadata.Indexes[rowSet.ObjectId]
                            .FirstOrDefault(i => i.IndexId == rowSet.IndexId);

        var parentIndex = metadata.Indexes[internalObject.ObjectId]
                                  .FirstOrDefault(i => i.IndexId <= 1);

        var displayName = !string.IsNullOrEmpty(index?.Name)
            ? $"{schema.Name}.{internalObject.Name}.{index.Name}"
            : $"{schema.Name}.{internalObject.Name}";

        var allocationUnit = new AllocationUnit
        {
            AllocationUnitId = source.AllocationUnitId,
            AllocationUnitType = (AllocationUnitType)source.Type,
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
            CompressionType = (CompressionType)rowSet.CompressionType,
            ParentIndexType = parentIndex?.IndexType
        };

        return allocationUnit;
    }
}
