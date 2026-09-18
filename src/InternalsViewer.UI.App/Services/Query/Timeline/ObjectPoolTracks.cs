using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.Query.Events.BatchMode.Enums;

namespace InternalsViewer.UI.App.Services.Query.Timeline;

internal sealed class ObjectPoolTracks
{
    private int[] _tracks = [];

    private int[] _laneStarts = [];

    public int TrackCount { get; private set; } = 1;

    public IReadOnlyList<int> LaneStarts => _laneStarts;

    public int TrackOf(int eventIndex) => eventIndex >= 0 && eventIndex < _tracks.Length ? _tracks[eventIndex] : 0;

    public float MinBandHeight(float bandPadding) => TrackCount * SegmentScanTracks.MinTrackHeight * 2 + bandPadding * 2;

    public void Rebuild(IReadOnlyList<EngineEvent> events)
    {
        _tracks = new int[events.Count];

        var laneStarts = new List<int>();

        var next = 0;

        foreach (var isDictionaryLane in new[] { false, true })
        {
            var laneTracks = BuildLane(events, isDictionaryLane, next);

            if (laneTracks == 0)
            {
                continue;
            }

            laneStarts.Add(next);

            next += laneTracks;
        }

        _laneStarts = [.. laneStarts];

        TrackCount = Math.Max(1, next);
    }

    private int BuildLane(IReadOnlyList<EngineEvent> events, bool isDictionaryLane, int laneStart)
    {
        var objects = new Dictionary<(ulong Hobt, int Column, ColumnStoreObjectType Type), int>();

        var rowGroups = new Dictionary<(ulong Hobt, long? RowGroup), RowGroupSpan>();

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is not ObjectPoolEvent pool || IsDictionary(pool) != isDictionaryLane)
            {
                continue;
            }

            var objectKey = (pool.HobtId, pool.ColumnId, pool.ObjectType);

            if (!objects.ContainsKey(objectKey))
            {
                objects[objectKey] = objects.Count;
            }

            var rowGroupKey = (pool.HobtId, pool.RowGroupId);

            if (!rowGroups.TryGetValue(rowGroupKey, out var span))
            {
                span = new RowGroupSpan();

                rowGroups[rowGroupKey] = span;
            }

            span.Add(i, pool.TimeUs, pool.TimeUs + pool.DurationUs);
        }

        if (objects.Count == 0)
        {
            return 0;
        }

        var stackEnds = new List<long>();

        foreach (var span in rowGroups.Values.OrderBy(s => s.StartUs))
        {
            var stack = FreeStack(stackEnds, span.StartUs);

            stackEnds[stack] = Math.Max(stackEnds[stack], span.EndUs);

            foreach (var index in span.Indexes)
            {
                var pool = (ObjectPoolEvent)events[index];

                _tracks[index] = laneStart + stack * objects.Count + objects[(pool.HobtId, pool.ColumnId, pool.ObjectType)];
            }
        }

        return stackEnds.Count * objects.Count;
    }

    private static int FreeStack(List<long> stackEnds, long startUs)
    {
        for (var stack = 0; stack < stackEnds.Count; stack++)
        {
            if (stackEnds[stack] <= startUs)
            {
                return stack;
            }
        }

        stackEnds.Add(startUs);

        return stackEnds.Count - 1;
    }

    private static bool IsDictionary(ObjectPoolEvent pool)
        => pool.ObjectType is ColumnStoreObjectType.PrimaryDictionary
                           or ColumnStoreObjectType.SecondaryDictionary
                           or ColumnStoreObjectType.BulkInsertDictionary;

    private sealed class RowGroupSpan
    {
        public List<int> Indexes { get; } = [];

        public long StartUs { get; private set; } = long.MaxValue;

        public long EndUs { get; private set; } = long.MinValue;

        public void Add(int index, long startUs, long endUs)
        {
            Indexes.Add(index);

            StartUs = Math.Min(StartUs, startUs);

            EndUs = Math.Max(EndUs, endUs);
        }
    }
}
