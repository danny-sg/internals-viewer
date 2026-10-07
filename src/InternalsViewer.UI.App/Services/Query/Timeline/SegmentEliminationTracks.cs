using System.Collections.Generic;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;

namespace InternalsViewer.UI.App.Services.Query.Timeline;

internal sealed class SegmentEliminationTracks
{
    private int[] _tracks = [];

    private int[] _trackCounts = [];

    public int TrackCount { get; private set; } = 1;

    public int TrackOf(int eventIndex) => eventIndex >= 0 && eventIndex < _tracks.Length ? _tracks[eventIndex] : 0;

    public int TrackCountOf(int eventIndex)
        => eventIndex >= 0 && eventIndex < _trackCounts.Length && _trackCounts[eventIndex] > 0 ? _trackCounts[eventIndex] : 1;

    public void Rebuild(IReadOnlyList<EngineEvent> events)
    {
        _tracks = new int[events.Count];

        _trackCounts = new int[events.Count];

        TrackCount = 1;

        var runs = new Dictionary<ulong, List<int>>();

        for (var i = 0; i < events.Count; i++)
        {
            var engineEvent = events[i];

            if (engineEvent is SegmentScanEvent or RowGroupScanEvent)
            {
                if (runs.TryGetValue(TaskOf(engineEvent), out var scanned))
                {
                    Close(scanned);
                }

                continue;
            }

            if (engineEvent is not SegmentEliminateEvent eliminate)
            {
                continue;
            }

            var task = TaskOf(eliminate);

            if (!runs.TryGetValue(task, out var run))
            {
                run = [];

                runs[task] = run;
            }

            if (run.Count > 0 && ((SegmentEliminateEvent)events[run[^1]]).HobtId != eliminate.HobtId)
            {
                Close(run);
            }

            run.Add(i);
        }

        foreach (var run in runs.Values)
        {
            Close(run);
        }
    }

    private static ulong TaskOf(EngineEvent engineEvent) => engineEvent.TaskAddress ?? (ulong)engineEvent.ThreadId;

    private void Close(List<int> run)
    {
        for (var track = 0; track < run.Count; track++)
        {
            _tracks[run[track]] = track;

            _trackCounts[run[track]] = run.Count;
        }

        if (run.Count > TrackCount)
        {
            TrackCount = run.Count;
        }

        run.Clear();
    }
}
