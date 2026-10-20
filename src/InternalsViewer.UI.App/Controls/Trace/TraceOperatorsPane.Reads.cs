using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.UI.App.Controls.Timeline;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed partial class TraceOperatorsPane
{
    private const string ReadsTitle = "Reads";

    private const int ReadLevel = 0;

    public TraceReads? Reads { get; set; }

    public bool ShowReads { get; set; } = true;

    public TraceRead? GetReadAt(Point position)
        => GetStripAt<TraceRead>(position, GetReads, GetReadLevel) is { } strip ? GetNearestMark(strip.Marks, position) : null;

    private TraceOperatorHit? GetReadStripAt(Point position) => GetStripAt<TraceRead>(position, GetReads, GetReadLevel)?.Hit;

    private TraceMarkIndex<TraceRead>? GetReads(TraceOperatorRow row, TraceOperatorLifetime lifetime)
        => ShowReads ? Reads?.GetReads(row.Operator, lifetime.Thread) : null;

    private bool HasReadStrip(TraceOperatorRow row, TraceOperatorLifetime lifetime) => GetReads(row, lifetime) is not null;

    private static int GetReadLevel(TraceOperatorRow row, TraceOperatorLifetime lifetime) => ReadLevel;

    private void DrawReadStrip(SKCanvas canvas,
                               TraceOperatorRow row,
                               TraceOperatorLifetime lifetime,
                               SKRect bar,
                               SKColor colour,
                               double scale)
    {
        if (GetReads(row, lifetime) is not { } reads || GetStrip(bar, ReadLevel) is not { } strip)
        {
            return;
        }

        DrawStrip(canvas,
                  bar,
                  strip,
                  TimelineColours.Scale(colour, ReturnStripShade),
                  reads,
                  [_ => colour],
                  r => r.Type == TraceReadType.Page,
                  scale);
    }

    private void DrawReadHover(SKCanvas canvas, double scale)
        => DrawMarkHover(canvas, Hover.Read, Hover.Read?.Operator, Hover.Read?.Thread ?? 0, GetReads, GetReadLevel, scale);

    private string GetReadToolTip(TraceRead read)
    {
        var lines = new List<string>
        {
            ReadsTitle,
            read.Type == TraceReadType.ReadAhead
                ? $"Read Ahead {read.PageCount:N0} {(read.PageCount == 1 ? "Page" : "Pages")} From {read.Page}"
                : $"Page {read.Page}",
            read.Node.Symbol,
            $"Thread {read.Thread}  ·  Call {read.Call + 1:N0}",
            GetMarkPosition(read.Position)
        };

        return string.Join(Environment.NewLine, lines);
    }

    private string GetReadStripToolTip(TraceOperatorHit hit)
    {
        var reads = GetReads(Rows[hit.Row], Rows[hit.Row].Lifetimes[hit.Track])?.All ?? [];

        var lines = new List<string> { ReadsTitle, $"Page Reads {reads.Count(r => r.Type == TraceReadType.Page):N0}" };

        var readAheads = reads.Where(r => r.Type == TraceReadType.ReadAhead).ToList();

        if (readAheads.Count > 0)
        {
            lines.Add($"Read Aheads {readAheads.Count:N0} ({readAheads.Sum(r => (long)r.PageCount):N0} Pages)");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
