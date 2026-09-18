using System;
using System.Collections.Generic;
using SkiaSharp;

namespace InternalsViewer.UI.App.Controls.Timeline.Definition;

public sealed record TimelineBand(Type Key, string Label, SKColor Colour, float Weight)
{
    public TimelineSubBandLabels? SubBandLabels { get; init; }

    public float MinInnerHeight { get; init; }

    public IReadOnlyList<TimelineTrackDivider> TrackDividers { get; init; } = [];
}

public readonly record struct TimelineSubBandLabels(string Top, string Middle, string Bottom);

public readonly record struct TimelineTrackDivider(int Track, int TrackCount);
