using System;
using InternalsViewer.Internals.Engine.Database;
using IndexType = InternalsViewer.Internals.Engine.Database.Enums.IndexType;

namespace InternalsViewer.UI.App.Helpers;

internal static class AllocationUnitHelpers
{
    public static string DisplayName(this AllocationUnit unit)
        => string.IsNullOrEmpty(unit.IndexName) ? unit.TableName ?? string.Empty : unit.IndexName;

    public static string IndexTypeName(this AllocationUnit unit)
    {
        return GetIndexTypeName(unit.IndexType);
    }

    public static string GetIndexTypeName(IndexType unitIndexType)
    {
        return unitIndexType switch
        {
            IndexType.Heap => "Heap",
            IndexType.Clustered => "Clustered",
            IndexType.NonClustered => "Non-Clustered",
            IndexType.Xml => "XML",
            IndexType.Spatial => "Spatial",
            IndexType.ClusteredColumnStore => "Clustered Columnstore",
            IndexType.NonClusteredColumnStore => "Non-Clustered Columnstore",
            IndexType.NonClusteredHash => "Non-Clustered Hash",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}