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

    private const byte HitLayer = 1;

    private static readonly TimelineBand Band = new(typeof(SegmentScanEvent),
                                                    "Columnstore",
                                                    ColourConstants.SegmentColour.ToSkColor().WithAlpha(255),
                                                    0.5f)
    {
        SubBandLabels = new TimelineSubBandLabels("Rowgroup", "Columnstore", "Object Pool"),
    };

    private static readonly SKColor SegmentEliminationColour = ColourConstants.SegmentEliminationColour.ToSkColor();

    private static readonly SKColor ObjectPoolHitColour = ColourConstants.ObjectPoolHitColour.ToSkColor();

    private static readonly SKColor ObjectPoolMissColour = ColourConstants.ObjectPoolMissColour.ToSkColor();

    private static readonly SKColor ColumnStoreEventColour = ColourConstants.ColumnStoreEventColour.ToSkColor();

    private static readonly SKColor ExpressionFilterBitmapColour = ColourConstants.ExpressionFilterBitmapColour.ToSkColor();

    private SegmentScanTracks SegmentTracks { get; } = new();

    private ObjectPoolTracks PoolTracks { get; } = new();

    public bool Claims(EngineEvent engineEvent)
        => engineEvent is SegmentScanEvent or SegmentEliminateEvent or ObjectPoolEvent or ColumnStoreScanEvent;

    public TimelineBand? Prepare(IReadOnlyList<EngineEvent> events, TimelineBandVisibility visibility)
    {
        if (!events.Any(Claims))
        {
            return null;
        }

        SegmentTracks.Rebuild(events);

        PoolTracks.Rebuild(events);

        var dividers = new TimelineTrackDivider[PoolTracks.LaneStarts.Count];

        for (var lane = 0; lane < dividers.Length; lane++)
        {
            dividers[lane] = new TimelineTrackDivider(PoolTracks.TrackCount + PoolTracks.LaneStarts[lane], PoolTracks.TrackCount * 2);
        }

        return Band with
        {
            MinInnerHeight = Math.Max(SegmentTracks.TrackCount, PoolTracks.TrackCount) * SegmentScanTracks.MinTrackHeight * 2,
            TrackDividers = dividers,
        };
    }

    public TimelineItem Place(int index, EngineEvent engineEvent, int band, List<TimelineLink> links) => engineEvent switch
    {
        SegmentScanEvent => new TimelineItem(band,
                                             SegmentTracks.TrackOf(index),
                                             SegmentTracks.TrackCount * 2,
                                             2,
                                             TimelineFill.Solid,
                                             TimelineTickAnchor.Start,
                                             TimelineColourSource.Fixed,
                                             Band.Colour,
                                             0f,
                                             0,
                                             MinPoolHitWidth),
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
        SegmentEliminateEvent => Half(band, 0, SegmentEliminationColour),
        ColumnStoreScanEvent scan => Half(band, scan.IsRowGroupEvent ? 0 : 1, ScanColour(scan)),
        _ => TimelineItem.Undrawn(band),
    };

    private static TimelineItem Half(int band, int half, SKColor colour)
        => new(band, half, 2, 1, TimelineFill.Translucent, TimelineTickAnchor.Start, TimelineColourSource.Fixed, colour, 0f);

    private static SKColor ScanColour(ColumnStoreScanEvent scan)
        => scan.EventName == "column_store_expression_filter_bitmap_set" ? ExpressionFilterBitmapColour : ColumnStoreEventColour;
}
