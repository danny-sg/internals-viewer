using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Assigns each object pool lookup a lane within the Pool half of the Columnstore row, one lane per pooled object
/// </summary>
/// <remarks>
/// A rowgroup's objects, its column segments and secondary dictionaries, take the low lanes in the order
/// they are first seen, so a hit and a miss for the same object share a lane and different objects stack whether or not
/// they overlap in time. Primary dictionaries and the delete bitmap are shared across rowgroups, so they take their own
/// lanes above the rowgroup lanes, one per object. The lane count is the widest rowgroup plus the shared objects seen.
/// </remarks>
internal sealed class ObjectPoolLanes
{
    private int[] _lanes = [];

    public int LaneCount { get; private set; } = 1;

    public int LaneOf(int eventIndex) => eventIndex >= 0 && eventIndex < _lanes.Length ? _lanes[eventIndex] : 0;

    public float MinRowHeight(float rowPadding) => LaneCount * SegmentScanLanes.MinLaneHeight * 2 + rowPadding * 2;

    public void Rebuild(IReadOnlyList<EngineEvent> events)
    {
        _lanes = new int[events.Count];

        var rowGroupLanes = new Dictionary<(ulong Hobt, long RowGroup), Dictionary<(int Column, ColumnStoreObjectType Type), int>>();

        var sharedLanes = new Dictionary<(ulong Hobt, int Column, ColumnStoreObjectType Type), int>();

        var rowGroupLaneCount = 0;

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is not ObjectPoolEvent pool)
            {
                continue;
            }

            if (IsShared(pool))
            {
                var sharedKey = (pool.HobtId, pool.ColumnId, pool.ObjectType);

                if (!sharedLanes.TryGetValue(sharedKey, out var sharedLane))
                {
                    sharedLane = sharedLanes.Count;

                    sharedLanes[sharedKey] = sharedLane;
                }

                _lanes[i] = sharedLane;

                continue;
            }

            var rowGroupKey = (pool.HobtId, pool.RowGroupId ?? -1);

            if (!rowGroupLanes.TryGetValue(rowGroupKey, out var objects))
            {
                objects = [];

                rowGroupLanes[rowGroupKey] = objects;
            }

            var objectKey = (pool.ColumnId, pool.ObjectType);

            if (!objects.TryGetValue(objectKey, out var lane))
            {
                lane = objects.Count;

                objects[objectKey] = lane;
            }

            _lanes[i] = lane;

            rowGroupLaneCount = Math.Max(rowGroupLaneCount, objects.Count);
        }

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is ObjectPoolEvent pool && IsShared(pool))
            {
                _lanes[i] += rowGroupLaneCount;
            }
        }

        LaneCount = Math.Max(1, rowGroupLaneCount + sharedLanes.Count);
    }

    private static bool IsShared(ObjectPoolEvent pool)
        => pool.ObjectType is ColumnStoreObjectType.PrimaryDictionary or ColumnStoreObjectType.DeleteBitmap;
}
