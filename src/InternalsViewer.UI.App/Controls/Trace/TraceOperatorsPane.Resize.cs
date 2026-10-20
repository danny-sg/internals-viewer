using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.UI.App.Controls.Trace.Enums;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed partial class TraceOperatorsPane
{
    private const float ResizeHitMargin = 2f;

    private const float MinimumRowHeight = 4f;

    public bool IsResizing => ActiveResize is not null;

    private Dictionary<ExecutionOperatorEvent, float> RowHeights { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<string, float> BandHeights { get; } = new(StringComparer.Ordinal);

    private (TraceResizeHandle Handle, double PressY)? ActiveResize { get; set; }

    public TraceResizeHandle? GetResizeHandle(Point position)
    {
        if (!Contains(position))
        {
            return null;
        }

        var layout = GetLayout();

        var y = (float)position.Y;

        for (var row = 0; row < Rows.Length; row++)
        {
            var bottom = layout.Tops[row] + layout.Heights[row];

            if (y >= bottom - ResizeHitMargin / 2 && y <= bottom + ResizeHitMargin * 2)
            {
                return new TraceResizeHandle(row, null, layout.Heights[row], Height);
            }
        }

        return GetBandHandle(position);
    }

    public void BeginResize(TraceResizeHandle handle, double y) => ActiveResize = (handle, y);

    public void Resize(double y)
    {
        if (ActiveResize is not { } active)
        {
            return;
        }

        var (handle, pressY) = active;

        var delta = (float)(y - pressY);

        if (handle.Track is { } track)
        {
            var height = track.Location == TraceTickLocation.Top ? handle.StartHeight + delta : handle.StartHeight - delta;

            BandHeights[track.Name] = Math.Clamp(height, MinimumStripHeight, handle.MaximumHeight);
        }
        else
        {
            RowHeights[Rows[handle.Row].Operator] = Math.Clamp(handle.StartHeight + delta, MinimumRowHeight, handle.MaximumHeight);
        }

        LayoutVersion++;
    }

    public void EndResize() => ActiveResize = null;

    public void Reset()
    {
        RowHeights.Clear();

        BandHeights.Clear();

        StatementEvent = null;

        LayoutVersion++;
    }

    private TraceResizeHandle? GetBandHandle(Point position)
    {
        if (GetOperatorAt(position) is not { } hit
            || GetTrackRect(hit.Row, hit.Track, Host.Width, Host.Scale) is not { } bar
            || position.X < bar.Left
            || position.X > bar.Right)
        {
            return null;
        }

        var y = (float)position.Y;

        var strips = GetStrips(hit.Row, hit.Track, bar);

        foreach (var strip in strips)
        {
            var edge = strip.Track.Location == TraceTickLocation.Top ? strip.Top + strip.Height : strip.Top;

            if (Math.Abs(y - edge) > ResizeHitMargin)
            {
                continue;
            }

            var others = 0f;

            foreach (var other in strips)
            {
                others += ReferenceEquals(other.Track, strip.Track) ? 0 : other.Height;
            }

            return new TraceResizeHandle(hit.Row, strip.Track, strip.Height, Math.Max(bar.Height - others, MinimumStripHeight));
        }

        return null;
    }
}
