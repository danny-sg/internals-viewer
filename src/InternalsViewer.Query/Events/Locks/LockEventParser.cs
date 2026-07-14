using System.Buffers.Binary;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Locks;

internal class LockEventParser: IEventParser<LockEvent>
{
    /// <summary>
    /// Parses a lock event
    /// </summary>
    /// <remarks>
    /// Defines a relationship between a lock owner, the lock mode, and the resource being locked:
    /// 
    ///     Lock owner --> [Lock Mode] --> Lock Resource
    /// </remarks>
    public static LockEvent Map(DatabaseSource databaseSource, EventResult e)
    {
        var lockMode = (LockMode)(e.GetInt("mode") ?? 0);

        var duration = e.GetLong("duration") ?? 0;

        var lockEvent = new LockEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            DurationUs = duration,
            LockMode = lockMode,
            Resource = ParseResource(e),
            LockOwnerContext = ParseLockOwnerContext(e),
        };

        lockEvent.AllocationUnit = lockEvent.Resource.ObjectId > 0
            ? databaseSource.FindObjectIdAllocationUnit(lockEvent.Resource.ObjectId)
            : lockEvent.Resource.HobtId is { } hobtId and > 0
                ? databaseSource.FindHobtIdAllocationUnit(hobtId)
                : null;

        return lockEvent;
    }

    /// <summary>
    /// Parses the lock owner context from the event
    /// </summary>
    /// <remarks>
    /// The lock owner context is the context that owns a lock, identifying the source of the lock request.
    /// </remarks>
    private static LockOwnerContext? ParseLockOwnerContext(EventResult e)
    {
        var ownerType = (LockOwnerType) (e.GetInt("owner_type") ?? 0);

        var transactionId = e.GetLong("transaction_id") ?? 0;
        var workspaceId = e.GetUlong("lockspace_workspace_id") ?? 0;
        var subId = e.GetUInt("lockspace_sub_id") ?? 0;
        var nestId = e.GetUInt("lockspace_nest_id") ?? 0;

        return new LockOwnerContext
        {
            OwnerType = ownerType,
            TransactionId = transactionId,
            WorkspaceId = workspaceId,
            SubId = subId,
            NestId = nestId
        };
    }

    /// <summary>
    /// Parse lock resource from the event
    /// </summary>
    /// <remarks>
    /// A lock is always put on a resource to protect it.
    /// 
    /// +-----------------+-----------------------------+-----------------------------+----------------------+---------------------+
    /// | Resource Type   | resource_0                  | resource_1                  | resource_2           | associated_object_id|
    /// +-----------------+-----------------------------+-----------------------------+----------------------+---------------------+
    /// | DATABASE        | Database ID                 | 0                           | 0                    | 0                   |
    /// | FILE            | File ID                     | File subresource            | 0                    | 0                   |
    /// | OBJECT          | Object ID                   | Lock partition              | 0                    | Object ID           |
    /// | HOBT            | HoBT ID(encoded part)       | HoBT ID (encoded part)      | 0                    | HoBT ID             |
    /// | PAGE            | Page ID                     | File ID                     | 0                    | HoBT ID             |
    /// | EXTENT          | Extent ID                   | File ID                     | 0                    | HoBT ID             |
    /// | RID             | Page ID                     | Encoded File ID + Slot ID   | 0                    | HoBT ID             |
    /// | KEY             | Encoded key hash            | 0                           | HoBT ID              |                     |
    /// | ALLOCATION_UNIT | Allocation Unit Id          | Internal / varies           | Internal / varies    | Allocation Unit ID  |
    /// | METADATA        | Internal metadata identifier| Internal metadata identifier| Internal identifier  | Internal / varies   |
    /// | APPLICATION     | Identifier/hash             | Internal / varies           | Internal / varies    | Usually 0           |
    /// | XACT            | Transaction identifier      | Internal / varies           | Internal / varies    | Transaction related |
    /// | OIB             | Internal OIB resource       | Internal OIB resource       | Internal OIB         | Internal / varies   |
    /// | ROW_GROUP       | Rowgroup Id                 | Internal / varies           | Rowgroup / partition |                     | 
    /// +-----------------+-----------------------------+-----------------------------+----------------------+---------------------+
    ///
    /// OIB = Online Index Build (out of scope)
    /// </remarks>
    private static LockResource ParseResource(EventResult e)
    {
        var resourceType = (LockResourceType)(e.GetInt("resource_type") ?? 0);

        var resource0 = e.GetUlong("resource_0") ?? 0;
        var resource1 = e.GetUlong("resource_1") ?? 0;
        var resource2 = e.GetUlong("resource_2") ?? 0;

        var resourceKey = resource0
                          ^ (resource1 * 0x9E3779B97F4A7C15UL)
                          ^ (resource2 * 0xC2B2AE3D27D4EB4FUL)
                          ^ ((ulong)(int)resourceType << 5);

        var objectId = e.GetLong("object_id") ?? 0;
        var associatedObjectId = e.GetLong("associated_object_id") ?? 0;

        var lockResource = new LockResource
        {
            ResourceType = resourceType,
            Key = resourceKey,
            ObjectId = (int)objectId,
            HobtId = associatedObjectId,
        };

        return resourceType switch
        {
            LockResourceType.Page =>
                lockResource with
                {
                    PageAddress = new PageAddress((short)resource1, (int)resource0)
                },
            LockResourceType.Rid =>
                lockResource with
                {
                    RowIdentifier = new RowIdentifier((short)resource0, (int)resource1, (ushort)resource2),
                },
            LockResourceType.Key =>
                lockResource with
                {
                    KeyHash = BuildKeyHash(resource1, resource2)
                },

            _ => lockResource
        };
    }

    /// <summary>
    /// Builds a KEY lock's <c>%%lockres%%</c> hash string from its resource DWORDs
    /// </summary>
    /// <remarks>
    /// Decode is:
    /// 
    ///     High 2 bytes = resource_1 (big-endian/byte-swapped)
    ///     Low 4 bytes = resource_2 (big-endian/byte-swapped)
    /// 
    /// </remarks>
    internal static string BuildKeyHash(ulong resource1, ulong resource2)
    {
        var high = BinaryPrimitives.ReverseEndianness((ushort)(resource1 >> 16));

        var low = BinaryPrimitives.ReverseEndianness((uint)resource2);

        var hash = ((ulong)high << 32) | low;

        return $"({hash:x12})";
    }
}
