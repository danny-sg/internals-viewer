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
    /// <summary>
    /// Builds the allocation units the metadata describes, leaving out any whose rowset it no longer holds
    /// </summary>
    /// <remarks>
    /// An allocation unit outlives its rowset while a drop is being cleaned up in the background, and its container id
    /// reads as zero for as long as that takes. There is nothing left to name it after, so it is left out rather than
    /// the whole of the metadata failing to load over it.
    /// </remarks>
    public static List<AllocationUnit> GetAllocationUnits(InternalMetadata metadata)
    {
        return [.. metadata.AllocationUnits.Values
                           .Where(a => metadata.Rowsets.ContainsKey(a.ContainerId))
                           .Select(a => GetAllocationUnit(metadata, a))];
    }

    public static AllocationUnit GetAllocationUnit(InternalMetadata metadata, InternalAllocationUnit source)
    {
        var rowset = metadata.Rowsets[source.ContainerId];

        var internalObject = metadata.Objects[rowset.ObjectId];

        var schema = metadata.Entities[(internalObject.SchemaId, (byte)MetadataConstants.SchemaClassId)];

        var index = metadata.Indexes[rowset.ObjectId]
                            .FirstOrDefault(i => i.IndexId == rowset.IndexId);

        var parentIndex = metadata.Indexes[internalObject.ObjectId]
                                  .FirstOrDefault(i => i.IndexId <= 1);

        var displayName = !string.IsNullOrEmpty(index?.Name)
            ? $"{schema.Name}.{internalObject.Name}.{index.Name}"
            : $"{schema.Name}.{internalObject.Name}";

        var allocationUnit = new AllocationUnit
        {
            AllocationUnitId = source.AllocationUnitId,
            AllocationUnitType = (AllocationUnitType)source.Type,
            ObjectId = rowset.ObjectId,
            IndexId = rowset.IndexId,
            SchemaName = schema.Name,
            TableName = internalObject.Name,
            IndexName = index?.Name ?? string.Empty,
            IndexType = index?.IndexType ?? 0,
            IsSystem = (internalObject.Status & 1) != 0,
            PartitionId = source.ContainerId,
            OwnerType = rowset.OwnerType,
            FirstPage = PageAddressParser.Parse(source.FirstPage!),
            RootPage = PageAddressParser.Parse(source.RootPage!),
            FirstIamPage = PageAddressParser.Parse(source.FirstIamPage!),
            UsedPages = source.UsedPages,
            TotalPages = source.TotalPages,
            DisplayName = displayName,
            CompressionType = (CompressionType)rowset.CompressionType,
            ParentIndexType = parentIndex?.IndexType
        };

        return allocationUnit;
    }
}
