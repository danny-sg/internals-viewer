using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.Properties;
using InternalsViewer.Query.FullTrace.Arguments.Enums;

namespace InternalsViewer.Query.FullTrace.Arguments;

public sealed record StorageId(StorageIdType Type, AllocationUnit AllocationUnit)
{
    private const string HoBtName = "HoBT";

    private const string AllocationUnitName = "Allocation Unit";

    public string Name { get; } = GetName(Type, AllocationUnit);

    public string TypeName => Type == StorageIdType.HoBt ? HoBtName : AllocationUnitName;

    public ulong HoBtId => (ulong)AllocationUnit.PartitionId;

    private static string GetName(StorageIdType type, AllocationUnit allocationUnit)
    {
        var name = allocationUnit.IndexName.Length == 0
            ? allocationUnit.TableName
            : $"{allocationUnit.TableName}.{allocationUnit.IndexName}";

        return type == StorageIdType.HoBt
            ? name
            : $"{name} ({EventPropertyFormatter.SplitWords(allocationUnit.AllocationUnitType.ToString())})";
    }
}
