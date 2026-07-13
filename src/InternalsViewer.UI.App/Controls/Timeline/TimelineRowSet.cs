using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.EventTypes;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events.Reads;
using InternalsViewer.Query.Events.Waits;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// The timeline's horizontal bands: which event type each lane holds, its label/colour/weight, which
/// lanes are shown for the current events and visibility flags, and the cached label text blobs
/// </summary>
internal sealed class TimelineRowSet : IDisposable
{
    public readonly record struct Row(Type EventType, string Label, SKColor Color, float Weight);

    // Rows below this event count use a wider marker so their sparse ticks stay easy to see.
    private const int SparseRowThreshold = 25;

    // Operator events span a duration and are drawn as lines, so the Plan row is given extra weight for its
    // per-level tracks. The Log row is dropped when there are no transaction-log events; the rest are always present.
    private static readonly Row[] AllRows =
    [
        new(typeof(TransactionLogEvent), "Log",  ColourConstants.LogColour.ToSkColor().WithAlpha(255),  0.5f),
        new(typeof(ExecutionOperatorEvent), "Plan", SKColors.LimeGreen, 3f),
        new(typeof(ReadEventGroup), "Read", ColourConstants.IoColour.ToSkColor().WithAlpha(255), 0.5f),
        new(typeof(LockEvent), "Lock", ColourConstants.LockColour.ToSkColor().WithAlpha(255), 0.5f),
        new(typeof(LatchEvent), "Latch", ColourConstants.LatchColour.ToSkColor().WithAlpha(255), 0.167f),
        new(typeof(WaitEvent), "Wait", ColourConstants.WaitColour.ToSkColor().WithAlpha(255), 0.5f),
    ];

    private Row[] _active = AllRows;
    private int[] _eventCounts = [];
    private SKTextBlob?[] _labelBlobs = [];

    public IReadOnlyList<Row> Active => _active;

    public void Rebuild(IReadOnlyList<EngineEvent> events,
                        bool showLocks,
                        bool showLatches,
                        bool showWaits,
                        SKFont labelFont)
    {
        var hasLog = events.Any(e => e is TransactionLogEvent);
        var hasLock = showLocks && events.Any(e => e is LockEvent or LockGroup);
        var hasLatch = showLatches && events.Any(e => e is LatchEvent);
        var hasWait = showWaits && events.Any(e => e is WaitEvent);

        _active = AllRows.Where(r =>
            (r.EventType != typeof(TransactionLogEvent) || hasLog) &&
            (r.EventType != typeof(LockEvent) || hasLock) &&
            (r.EventType != typeof(LatchEvent) || hasLatch) &&
            (r.EventType != typeof(WaitEvent) || hasWait)).ToArray();

        _eventCounts = new int[_active.Length];

        foreach (var ev in events)
        {
            var idx = IndexOf(ev);

            if (idx >= 0)
            {
                _eventCounts[idx]++;
            }
        }

        foreach (var blob in _labelBlobs)
        {
            blob?.Dispose();
        }

        _labelBlobs = new SKTextBlob?[_active.Length];

        for (var i = 0; i < _active.Length; i++)
        {
            _labelBlobs[i] = SKTextBlob.Create(_active[i].Label, labelFont, SKPoint.Empty);
        }
    }

    // The first active row whose event type the event is an instance of, or -1 when its lane isn't shown.
    public int IndexOf(EngineEvent ev)
    {
        for (var i = 0; i < _active.Length; i++)
            if (_active[i].EventType.IsInstanceOfType(ev))
            {
                return i;
            }

        return -1;
    }

    public int IndexOf(Type eventType)
    {
        for (var r = 0; r < _active.Length; r++)
        {
            if (_active[r].EventType == eventType)
            {
                return r;
            }
        }

        return -1;
    }

    public bool IsSparse(int rowIndex) =>
        rowIndex >= 0 && rowIndex < _eventCounts.Length && _eventCounts[rowIndex] < SparseRowThreshold;

    public SKTextBlob? LabelBlob(int rowIndex) =>
        rowIndex >= 0 && rowIndex < _labelBlobs.Length ? _labelBlobs[rowIndex] : null;

    public void Dispose()
    {
        foreach (var blob in _labelBlobs)
        {
            blob?.Dispose();
        }
    }
}
