using System.Linq;
using InternalsViewer.Query.Events.EventTypes;

namespace InternalsViewer.Query.Events.Locks;

/// <summary>
/// The locks one owner (transaction) took on a single object, capturing the escalation as its granularity moves up
/// (rid → page → object)
/// </summary>
/// <remarks>
/// Owns its constituent <see cref="LockEvent"/>s rather than duplicating them; a flatten/SelectMany over
/// <see cref="Events"/> expands the group back into the raw stream. The object it groups on is the resolved
/// <see cref="EngineEvent.AllocationUnit"/>'s object — the one identity shared across every granularity.
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

            return $"Locks on {target}: {string.Join(", ", byType)}";
        }
    }
}
