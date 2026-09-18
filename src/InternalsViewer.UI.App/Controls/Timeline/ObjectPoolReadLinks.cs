using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.Reads;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// The timeline index of the object pool lookup each read fed, so its rails can be drawn to the lookup rather than the
/// operator
/// </summary>
/// <remarks>
/// Which lookup a read belongs to is decided when the query is captured and carried on the read. This only turns that
/// reference into a position in the timeline's event list, which is what the renderers work in.
/// </remarks>
internal sealed class ObjectPoolReadLinks
{
    private int[] _poolOfRead = [];

    public int PoolIndexOf(int eventIndex)
        => eventIndex >= 0 && eventIndex < _poolOfRead.Length ? _poolOfRead[eventIndex] : -1;

    public void Rebuild(IReadOnlyList<EngineEvent> events)
    {
        _poolOfRead = new int[events.Count];

        Array.Fill(_poolOfRead, -1);

        var indexes = new Dictionary<ObjectPoolEvent, int>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is ObjectPoolEvent pool)
            {
                indexes[pool] = i;
            }
        }

        if (indexes.Count == 0)
        {
            return;
        }

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is ReadEventGroup { PoolLookup: { } lookup } && indexes.TryGetValue(lookup, out var index))
            {
                _poolOfRead[i] = index;
            }
        }
    }
}
