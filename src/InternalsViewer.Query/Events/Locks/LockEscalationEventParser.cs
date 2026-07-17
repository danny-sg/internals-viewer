using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Extensions;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Locks;

internal class LockEscalationEventParser : IEventParser<LockEscalationEvent>
{
    public static LockEscalationEvent? Map(DatabaseSource? databaseSource, EventResult e)
    {
        var objectId = (int)(e.GetLong("object_id") ?? 0);

        return new LockEscalationEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            LockMode = (LockMode)(e.GetInt("mode") ?? 0),
            ResourceType = (LockResourceType)(e.GetInt("resource_type") ?? 0),
            EscalatedObjectId = objectId,
            TransactionId = e.GetLong("transaction_id"),
            EscalatedLockCount = e.GetLong("escalated_lock_count") ?? 0,
            HobtLockCount = e.GetLong("hobt_lock_count") ?? 0,
            AllocationUnit = objectId > 0 ? databaseSource?.FindObjectIdAllocationUnit(objectId) : null,
        };
    }
}