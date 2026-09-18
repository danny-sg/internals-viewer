using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Controls.Timeline.Definition;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline;

/// <summary>
/// Timeline horizontal bands
/// </summary>
/// <remarks>
/// Defines which event type each lane holds, its label/colour/weight, which lanes are shown for the current events and visibility flags,
/// and the cached label text blobs
/// </remarks>
internal sealed class TimelineBandSet : IDisposable
{
    // Rows below this event count use a wider marker so their sparse ticks stay easy to see.
    private const int SparseBandThreshold = 25;

    private IReadOnlyList<TimelineBand> _active = [];
    private int[] _eventCounts = [];
    private SKTextBlob?[] _labelBlobs = [];

    public IReadOnlyList<TimelineBand> Active => _active;

    /// <summary>
    /// The width of the widest label among the shown rows, which is what the label gutter has to fit
    /// </summary>
    public float MaxLabelWidth { get; private set; }

    public void Rebuild(TimelineDefinition definition, SKFont labelFont)
    {
        _active = definition.Bands;

        _eventCounts = new int[_active.Count];

        foreach (var item in definition.Items)
        {
            if (item.Band >= 0)
            {
                _eventCounts[item.Band]++;
            }
        }

        foreach (var blob in _labelBlobs)
        {
            blob?.Dispose();
        }

        _labelBlobs = new SKTextBlob?[_active.Count];

        MaxLabelWidth = 0;

        for (var i = 0; i < _active.Count; i++)
        {
            _labelBlobs[i] = SKTextBlob.Create(_active[i].Label, labelFont, SKPoint.Empty);

            MaxLabelWidth = Math.Max(MaxLabelWidth, labelFont.MeasureText(_active[i].Label));
        }
    }

    public int IndexOf(Type eventType)
    {
        for (var r = 0; r < _active.Count; r++)
        {
            if (_active[r].Key == eventType)
            {
                return r;
            }
        }

        return -1;
    }

    public bool IsSparse(int bandIndex) =>
        bandIndex >= 0 && bandIndex < _eventCounts.Length && _eventCounts[bandIndex] < SparseBandThreshold;

    public SKTextBlob? LabelBlob(int bandIndex) =>
        bandIndex >= 0 && bandIndex < _labelBlobs.Length ? _labelBlobs[bandIndex] : null;

    public void Dispose()
    {
        foreach (var blob in _labelBlobs)
        {
            blob?.Dispose();
        }
    }
}
