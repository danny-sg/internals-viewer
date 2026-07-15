using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// The locks an owner (transaction) took on an object
/// </summary>
/// <remarks>
/// Captures the escalation as its granularity moves up (e.g. rid → page → object) and includes the constituent locks
/// </remarks>
public sealed record LockGroup : EngineEvent, IEventGroup
{
    public required IReadOnlyList<EngineEvent> Events { get; init; }

    public int LockCount => Events.Count;

    public override string Description
    {
        get
        {
            // Coarsest-first tally per resource type, e.g. "1 Object, 3 Page, 500 Key" — the shape of the escalation.
            var byType = Events.OfType<LockEvent>()
                               .GroupBy(l => l.Resource.ResourceType)
                               .OrderBy(g => g.Key)
                               .Select(g => $"{g.Count()} {g.Key}");

            var target = AllocationUnit is { } au ? $"{au.SchemaName}.{au.TableName}" : ObjectName;

            // Name distinguishes the data-lock chain ("Object Locks") from the schema locks ("Object Schema Locks").
            return $"{Name} on {target}: {string.Join(", ", byType)}";
        }
    }
}
