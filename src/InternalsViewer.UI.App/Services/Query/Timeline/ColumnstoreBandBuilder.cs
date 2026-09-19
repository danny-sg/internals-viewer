using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Events.BatchMode;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.ViewModels.Query;
using SkiaSharp;
using InternalsViewer.UI.App.Controls.Timeline.Definition;

namespace InternalsViewer.UI.App.Services.Query.Timeline;

internal sealed class ColumnstoreBandBuilder : ITimelineBandBuilder
{
    private const float MinPoolHitWidth = 4f;

    private const int SparseCount = 25;

    private const float SparseWidth = 4f;

    private const byte HitLayer = 1;

    private const byte SegmentScanAlpha = 160;

    private const byte FilterAlpha = 160;

    private static readonly TimelineBand Band = new(typeof(SegmentScanEvent),
                                                    "Columnstore",
                                                    ColourConstants.SegmentColour.ToSkColor().WithAlpha(255),
                                                    0.5f)
    {
        SubBandLabels = new TimelineSubBandLabels("Rowgroup", "Columnstore", "Object Pool"),
    };

    private static readonly SKColor SegmentScanColour = Band.Colour.WithAlpha(SegmentScanAlpha);

    private static readonly SKColor SegmentEliminationColour = ColourConstants.SegmentEliminationColour.ToSkColor();

    private static readonly SKColor ObjectPoolHitColour = ColourConstants.ObjectPoolHitColour.ToSkColor();

    private static readonly SKColor ObjectPoolMissColour = ColourConstants.ObjectPoolMissColour.ToSkColor();

    private static readonly SKColor ColumnStoreEventColour = ColourConstants.ColumnStoreEventColour.ToSkColor();

    private static readonly SKColor BatchFilterColour = ColourConstants.BatchFilterColour.ToSkColor().WithAlpha(FilterAlpha);

    private static readonly SKColor FilterApplyColour = ColourConstants.ExpressionFilterBitmapApplyColour.ToSkColor().WithAlpha(FilterAlpha);

    private SegmentScanTracks SegmentTracks { get; } = new();

    private ObjectPoolTracks PoolTracks { get; } = new();

    private Dictionary<int, int> ThreadLanes { get; } = [];

    private int LaneCount => Math.Max(1, ThreadLanes.Count);

    private float BatchFilterWidth { get; set; }

    private float FilterApplyWidth { get; set; }

    public bool Claims(EngineEvent engineEvent)
        => engineEvent is SegmentScanEvent
                           or SegmentEliminateEvent
                           or ObjectPoolEvent
                           or ColumnStoreScanEvent
                           or ColumnstoreFilterEvent
                           or RowGroupScanEvent;

    public TimelineBand? Prepare(IReadOnlyList<EngineEvent> events, TimelineBandVisibility visibility)
    {
        if (!events.Any(Claims))
        {
            return null;
        }

        BatchFilterWidth = SparseWidthFor(events.Count(e => e is ColumnstoreFilterEvent { IsBatchFilter: true }));

        FilterApplyWidth = SparseWidthFor(events.Count(e => e is ColumnstoreFilterEvent { IsBatchFilter: false }));

        SegmentTracks.Rebuild(events);

        PoolTracks.Rebuild(events);

        ThreadLanes.Clear();

        foreach (var thread in events.OfType<RowGroupScanEvent>().Select(g => g.ThreadId).Distinct().Order())
        {
            ThreadLanes[thread] = ThreadLanes.Count;
        }

        List<TimelineTrackDivider> dividers = [];

        for (var lane = 1; lane < LaneCount; lane++)
        {
            dividers.Add(new TimelineTrackDivider(lane, LaneCount * 2));
        }

        foreach (var laneStart in PoolTracks.LaneStarts)
        {
            dividers.Add(new TimelineTrackDivider(PoolTracks.TrackCount + laneStart, PoolTracks.TrackCount * 2));
        }

        var trackCount = Math.Max(LaneCount * SegmentTracks.TrackCount, PoolTracks.TrackCount);

        return Band with
        {
            MinInnerHeight = trackCount * SegmentScanTracks.MinTrackHeight * 2,
            TrackDividers = dividers,
        };
    }

    public TimelineItem Place(int index, EngineEvent engineEvent, int band, List<TimelineLink> links) => engineEvent switch
    {
        SegmentScanEvent scan => SegmentScan(band, index, scan),
        ObjectPoolEvent pool => new TimelineItem(band,
                                                 PoolTracks.TrackCount + PoolTracks.TrackOf(index),
                                                 PoolTracks.TrackCount * 2,
                                                 2,
                                                 TimelineFill.Solid,
                                                 TimelineTickAnchor.Start,
                                                 TimelineColourSource.Fixed,
                                                 pool.IsHit ? ObjectPoolHitColour : ObjectPoolMissColour,
                                                 pool.IsHit ? MinPoolHitWidth : 0f,
                                                 pool.IsHit ? HitLayer : (byte)0),
        SegmentEliminateEvent => Half(band, 0, SegmentEliminationColour, 0f),
        RowGroupScanEvent => Lane(band, engineEvent, ColumnStoreEventColour, 0f),
        ColumnstoreFilterEvent { IsBatchFilter: true } => Lane(band, engineEvent, BatchFilterColour, BatchFilterWidth),
        ColumnstoreFilterEvent => Lane(band, engineEvent, FilterApplyColour, FilterApplyWidth),
        ColumnStoreScanEvent { IsBitmapFilterSet: true } => TimelineItem.Undrawn(band),
        ColumnStoreScanEvent { IsRowGroupEvent: true } => Lane(band, engineEvent, ColumnStoreEventColour, 0f),
        ColumnStoreScanEvent => Half(band, 1, ColumnStoreEventColour, 0f),
        _ => TimelineItem.Undrawn(band),
    };

    private TimelineItem SegmentScan(int band, int index, SegmentScanEvent scan)
    {
        var laneCount = ThreadLanes.TryGetValue(scan.ThreadId, out var lane) ? LaneCount : 1;

        return new TimelineItem(band,
                                lane * SegmentTracks.TrackCount + SegmentTracks.TrackOf(index),
                                laneCount * SegmentTracks.TrackCount * 2,
                                2,
                                TimelineFill.Solid,
                                TimelineTickAnchor.Start,
                                TimelineColourSource.Fixed,
                                SegmentScanColour,
                                0f,
                                0,
                                MinPoolHitWidth);
    }

    private TimelineItem Lane(int band, EngineEvent engineEvent, SKColor colour, float minWidth)
        => ThreadLanes.TryGetValue(engineEvent.ThreadId, out var lane)
            ? Half(band, 0, colour, minWidth) with { Track = lane, TrackCount = LaneCount * 2 }
            : Half(band, 0, colour, minWidth);

    private static TimelineItem Half(int band, int half, SKColor colour, float minWidth)
        => new(band, half, 2, 1, TimelineFill.Translucent, TimelineTickAnchor.Start, TimelineColourSource.Fixed, colour, minWidth);

    private static float SparseWidthFor(int count) => count < SparseCount ? SparseWidth : 0f;
}
