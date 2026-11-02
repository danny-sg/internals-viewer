using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.UI.App.Controls.Query.Trace.Memory.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Memory;

public sealed partial class MemoryReferenceGraphControl
{
    private const float ArrowLength = 6;

    private const float ArrowHalfWidth = 3;

    private const float EdgeLabelGap = 4;

    private const float EdgeLabelRise = 15;

    private const float EdgeLabelDrop = 1;

    private const float LabelHeight = 13;

    private const int MaximumLabelShifts = 12;

    private const float EdgeHitTolerance = 4;

    private const float HoverWidth = 2;

    private const float DetailZoom = 0.8f;

    private const float BorderWidth = 1;

    private const float BandHeight = 4;

    private const float LifetimeHeight = 1;

    private const float ContentMarginLeft = 4;

    private const float ContentMarginRight = 2;

    private const float ToggleSize = 18;

    private const float ToggleRadius = 4;

    private const float ColumnSpacing = 4;

    private const float NameSize = 12;

    private const float DetailSize = 11;

    private const float LabelSize = 10;

    private const float NameLineHeight = 16;

    private const float DetailLineHeight = 14;

    private const float NameLineRatio = 1.35f;

    private const float MinimumNameSize = 9;

    private const float NameShare = 0.55f;

    private const float WatchValueGap = 8;

    private const float WatchValueShare = 0.5f;

    private const float ChevronHalfWidth = 2;

    private const float ChevronHalfHeight = 4;

    private const float ChevronThickness = 1.2f;

    private const float UnderlineGap = 2;

    private const string Ellipsis = "\u2026";

    private const int TrimmedLimit = 20000;

    private List<EdgeLabel> Labels { get; } = [];

    private List<SKRect> EdgeBounds { get; } = [];

    private List<SKRect> HoveredLabels { get; } = [];

    private Dictionary<(string Text, float Size, float Width, bool IsDetail), string> Trimmed { get; } = [];

    private SKColor?[] MarkColumns { get; set; } = [];

    private SKPathBuilder PathBuilder { get; } = new();

    private void BuildScene(MemoryReferenceGraph? graph)
    {
        Labels.Clear();

        EdgeBounds.Clear();

        Trimmed.Clear();

        if (graph is null)
        {
            return;
        }

        foreach (var edge in graph.Edges)
        {
            EdgeBounds.Add(GetBounds(edge.Points));

            var left = (float)edge.LabelAt.X + EdgeLabelGap;

            var y = (float)edge.LabelAt.Y;

            AddLabel(edge, false, left, edge.IsLabelBelow ? y + EdgeLabelDrop : y - EdgeLabelRise, edge.IsLabelBelow ? 1 : -1);

            if (edge.Changes.Length > 0)
            {
                AddLabel(edge, true, left, y + EdgeLabelDrop, 1);
            }
        }
    }

    private void AddLabel(MemoryReferenceGraphEdge edge, bool isChanges, float left, float top, int direction)
    {
        var width = Sized(Paints.LabelFont, LabelSize).MeasureText(isChanges ? edge.Changes : edge.WidestLabel);

        var bounds = new SKRect(left, top, left + width, top + LabelHeight);

        if (!edge.IsCrossLink)
        {
            for (var shift = 0; shift < MaximumLabelShifts && Labels.Exists(l => !l.IsHidden && l.Bounds.IntersectsWith(bounds)); shift++)
            {
                bounds.Offset(0, direction * LabelHeight);
            }
        }

        Labels.Add(new EdgeLabel(edge, isChanges, bounds));
    }

    private static SKRect GetBounds(Point[] points)
    {
        var bounds = new SKRect((float)points.Min(p => p.X),
                                (float)points.Min(p => p.Y),
                                (float)points.Max(p => p.X),
                                (float)points.Max(p => p.Y));

        bounds.Inflate(ArrowLength, ArrowLength);

        return bounds;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.Transparent);

        if (Graph is not { } graph)
        {
            return;
        }

        if (IsThemeDirty)
        {
            ApplyTheme();

            IsThemeDirty = false;
        }

        var view = new GraphView((float)GraphCanvas.Dpi,
                                 Viewport.Zoom,
                                 Viewport.OffsetX,
                                 Viewport.OffsetY,
                                 (float)GraphCanvas.ActualWidth,
                                 (float)GraphCanvas.ActualHeight);

        var isDetail = view.Zoom >= DetailZoom;

        DrawEdges(canvas, graph, view);

        if (isDetail)
        {
            canvas.Save();

            canvas.Scale(view.Scale);

            Viewport.Apply(canvas);

            DrawLabels(canvas, view);

            canvas.Restore();
        }

        var visible = graph.Nodes.Where(n => view.IsVisible(n, graph)).ToList();

        foreach (var node in visible)
        {
            DrawNode(canvas, node, graph, view, isDetail);
        }

        canvas.Save();

        canvas.Scale(view.Scale);

        Viewport.Apply(canvas);

        foreach (var node in visible)
        {
            DrawNodeContent(canvas, node, graph, view, isDetail);
        }

        canvas.Restore();

        if (IsSelecting)
        {
            DrawSelection(canvas, view);
        }
    }

    private void DrawEdges(SKCanvas canvas, MemoryReferenceGraph graph, GraphView view)
    {
        var hovered = HoverHit.Edge;

        for (var index = 0; index < graph.Edges.Count; index++)
        {
            var edge = graph.Edges[index];

            if (ReferenceEquals(edge, hovered) || !EdgeBounds[index].IntersectsWith(view.Visible))
            {
                continue;
            }

            var colour = edge.IsHot ? Paints.HotLine : edge.IsCrossLink ? Paints.CrossLine : Paints.Line;

            DrawEdge(canvas, edge, colour, view.Thickness, view);
        }

        if (hovered is not null)
        {
            DrawEdge(canvas, hovered, Paints.HoverLine, view.HoverThickness, view);
        }
    }

    private void DrawEdge(SKCanvas canvas, MemoryReferenceGraphEdge edge, SKColor colour, float thickness, GraphView view)
    {
        var points = edge.Points;

        Paints.Stroke.Color = colour;

        Paints.Stroke.StrokeWidth = thickness;

        if (edge.IsCrossLink)
        {
            DrawDashes(canvas, points, thickness, view);
        }
        else
        {
            PathBuilder.MoveTo(view.SnapX(points[0].X, thickness), view.SnapY(points[0].Y, thickness));

            for (var point = 1; point < points.Length; point++)
            {
                PathBuilder.LineTo(view.SnapX(points[point].X, thickness), view.SnapY(points[point].Y, thickness));
            }

            using var line = PathBuilder.Detach();

            canvas.DrawPath(line, Paints.Stroke);
        }

        var endX = view.SnapX(points[^1].X, thickness);

        var endY = view.SnapY(points[^1].Y, thickness);

        var direction = points[^1].X >= points[^2].X ? 1 : -1;

        var length = view.ToPixels(ArrowLength) * direction;

        var halfWidth = view.ToPixels(ArrowHalfWidth);

        PathBuilder.MoveTo(endX - length, endY - halfWidth);

        PathBuilder.LineTo(endX, endY);

        PathBuilder.LineTo(endX - length, endY + halfWidth);

        PathBuilder.Close();

        Paints.Shape.Color = colour;

        using var arrow = PathBuilder.Detach();

        canvas.DrawPath(arrow, Paints.Shape);
    }

    private void DrawDashes(SKCanvas canvas, Point[] points, float thickness, GraphView view)
    {
        var period = Paints.GetDashPeriod(view.Scale);

        var originX = MathF.Round(view.ToX(0));

        var originY = MathF.Round(view.ToY(0));

        for (var point = 1; point < points.Length; point++)
        {
            var x0 = view.SnapX(points[point - 1].X, thickness);

            var y0 = view.SnapY(points[point - 1].Y, thickness);

            var x1 = view.SnapX(points[point].X, thickness);

            var y1 = view.SnapY(points[point].Y, thickness);

            if (x0 == x1 && y0 == y1)
            {
                continue;
            }

            var isHorizontal = y0 == y1;

            if (isHorizontal ? x1 < x0 : y1 < y0)
            {
                (x0, y0, x1, y1) = (x1, y1, x0, y0);
            }

            var phase = (isHorizontal ? x0 - originX : y0 - originY) % period;

            Paints.Stroke.PathEffect = Paints.GetDash(view.Scale, phase < 0 ? phase + period : phase);

            canvas.DrawLine(x0, y0, x1, y1, Paints.Stroke);
        }

        Paints.Stroke.PathEffect = null;
    }

    private void DrawNode(SKCanvas canvas, MemoryReferenceGraphNode node, MemoryReferenceGraph graph, GraphView view, bool isDetail)
    {
        var left = MathF.Round(view.ToX(node.X));

        var top = MathF.Round(view.ToY(node.Y));

        var right = MathF.Round(view.ToX(node.X + graph.NodeWidth));

        var bottom = MathF.Round(view.ToY(node.Y + node.Height));

        var border = node.IsRoot ? view.Thickness * 2 : view.Thickness;

        if (ReferenceEquals(node, SelectedNode))
        {
            var outset = view.Thickness * 2;

            FillRect(canvas, Paints.Accent, left - outset, top - outset, right + outset, bottom + outset);
        }

        FillRect(canvas, node.IsRoot ? Paints.Accent : Paints.Line, left, top, right, bottom);

        FillRect(canvas, node.IsData ? Paints.DataNode : Paints.Node, left + border, top + border, right - border, bottom - border);

        if (isDetail)
        {
            DrawBand(canvas, node, view, left + border, top + border, right - border);
        }
    }

    private void DrawBand(SKCanvas canvas, MemoryReferenceGraphNode node, GraphView view, float left, float top, float right)
    {
        var width = right - left;

        if (width <= 0)
        {
            return;
        }

        var bandBottom = top + Math.Max(1, MathF.Round(view.ToPixels(BandHeight)));

        var lineBottom = bandBottom + Math.Max(1, MathF.Round(view.ToPixels(LifetimeHeight)));

        FillRect(canvas, Paints.Progress, left, bandBottom, right, lineBottom);

        foreach (var span in node.ActiveSpans)
        {
            var from = left + MathF.Round((float)span.Start * width);

            var to = Math.Max(from + 1, left + MathF.Round((float)span.End * width));

            var colour = span.Colour is { } found ? new SKColor(found.R, found.G, found.B, found.A) : Paints.Active;

            FillRect(canvas, colour, from, bandBottom, to, lineBottom);
        }

        FillRect(canvas, Paints.Progress, left, top, left + MathF.Round((float)Progress * width), bandBottom);

        var columns = (int)width;

        if (node.Marks.Length == 0 || columns <= 0)
        {
            return;
        }

        if (MarkColumns.Length < columns)
        {
            MarkColumns = new SKColor?[columns];
        }

        Array.Clear(MarkColumns, 0, columns);

        foreach (var mark in node.Marks)
        {
            var colour = mark.Colour;

            MarkColumns[Math.Clamp((int)(mark.At * columns), 0, columns - 1)] = new SKColor(colour.R, colour.G, colour.B, colour.A);
        }

        for (var column = 0; column < columns; column++)
        {
            if (MarkColumns[column] is not { } colour)
            {
                continue;
            }

            var start = column;

            while (column + 1 < columns && MarkColumns[column + 1] == colour)
            {
                column++;
            }

            FillRect(canvas, colour, left + start, top, left + column + 1, bandBottom);
        }
    }

    private void DrawNodeContent(SKCanvas canvas,
                                 MemoryReferenceGraphNode node,
                                 MemoryReferenceGraph graph,
                                 GraphView view,
                                 bool isDetail)
    {
        var layout = GetLayout(node, graph, view.Zoom, isDetail);

        var hovered = ReferenceEquals(HoverHit.Node, node) ? HoverHit.Part : MemoryReferenceGraphPart.None;

        if (!layout.Referrers.IsEmpty)
        {
            DrawToggle(canvas, layout.Referrers, !node.IsReferrersExpanded, hovered == MemoryReferenceGraphPart.Referrers);
        }

        if (!layout.Expand.IsEmpty)
        {
            DrawToggle(canvas, layout.Expand, node.IsExpanded, hovered == MemoryReferenceGraphPart.Expand);
        }

        var font = Sized(Paints.NameFont, layout.NameSize);

        var name = Trim(node.Label, font, layout.TextRight - layout.TextLeft, false);

        var nameWidth = font.MeasureText(name);

        var baseline = GetBaseline(font, layout.NameTop, layout.NameHeight);

        var colour = node.CanOpen ? Paints.Link : Paints.DisabledText;

        var deviceBaseline = DrawText(canvas, name, font, layout.TextLeft, baseline, colour, view);

        if (hovered == MemoryReferenceGraphPart.Name && node.CanOpen)
        {
            var left = view.ToX(layout.TextLeft);

            var underline = deviceBaseline + MathF.Round(view.ToPixels(UnderlineGap));

            canvas.Save();

            canvas.ResetMatrix();

            FillRect(canvas, colour, left, underline, left + view.ToPixels(nameWidth), underline + view.Thickness);

            canvas.Restore();
        }

        if (!isDetail)
        {
            return;
        }

        var detailFont = Sized(Paints.DetailFont, DetailSize);

        var detailTop = layout.NameTop + NameLineHeight;

        if (node.Detail.Length > 0)
        {
            DrawText(canvas,
                     Trim(node.Detail, detailFont, layout.TextRight - layout.TextLeft, true),
                     detailFont,
                     layout.TextLeft,
                     GetBaseline(detailFont, detailTop, DetailLineHeight),
                     Paints.SecondaryText,
                     view);
        }

        DrawWatches(canvas, node, graph, view, layout, detailFont, detailTop + DetailLineHeight);
    }

    private void DrawWatches(SKCanvas canvas,
                             MemoryReferenceGraphNode node,
                             MemoryReferenceGraph graph,
                             GraphView view,
                             NodeLayout layout,
                             SKFont font,
                             float top)
    {
        if (node.Watches.Length == 0 || node.WatchCount == 0)
        {
            return;
        }

        var width = layout.Right - layout.TextLeft;

        var lineHeight = (float)(node.Height - graph.NodeHeight) / node.WatchCount;

        foreach (var watch in node.Watches)
        {
            var baseline = GetBaseline(font, top, lineHeight);

            var nameWidth = font.MeasureText(watch.Name);

            var value = Trim(watch.Value, font, Math.Max(width - WatchValueGap - nameWidth, width * WatchValueShare), true);

            var valueWidth = font.MeasureText(value);

            var name = Trim(watch.Name, font, width - WatchValueGap - valueWidth, true);

            DrawText(canvas, name, font, layout.TextLeft, baseline, Paints.SecondaryText, view);

            DrawText(canvas, value, font, layout.Right - valueWidth, baseline, Paints.Text, view);

            top += lineHeight;
        }
    }

    private float DrawText(SKCanvas canvas, string text, SKFont font, float x, float baseline, SKColor colour, GraphView view)
    {
        var size = font.Size;

        var deviceBaseline = MathF.Round(view.ToY(baseline));

        font.Size = view.ToPixels(size);

        Paints.TextPaint.Color = colour;

        canvas.Save();

        canvas.ResetMatrix();

        canvas.DrawText(text, view.ToX(x), deviceBaseline, SKTextAlign.Left, font, Paints.TextPaint);

        canvas.Restore();

        font.Size = size;

        return deviceBaseline;
    }

    private static SKFont Sized(SKFont font, float size)
    {
        font.Size = size;

        return font;
    }

    private void DrawToggle(SKCanvas canvas, SKRect bounds, bool isLeft, bool isHovered)
    {
        if (isHovered)
        {
            Paints.Shape.Color = Paints.HoverFill;

            canvas.DrawRoundRect(bounds, ToggleRadius, ToggleRadius, Paints.Shape);
        }

        var x = bounds.MidX;

        var y = bounds.MidY;

        var tip = isLeft ? -ChevronHalfWidth : ChevronHalfWidth;

        PathBuilder.MoveTo(x - tip, y - ChevronHalfHeight);

        PathBuilder.LineTo(x + tip, y);

        PathBuilder.LineTo(x - tip, y + ChevronHalfHeight);

        Paints.Glyph.Color = Paints.Text;

        Paints.Glyph.StrokeWidth = ChevronThickness;

        using var chevron = PathBuilder.Detach();

        canvas.DrawPath(chevron, Paints.Glyph);
    }

    private void DrawLabels(SKCanvas canvas, GraphView view)
    {
        var font = Sized(Paints.LabelFont, LabelSize);

        var hovered = HoverHit.Edge;

        HoveredLabels.Clear();

        foreach (var label in Labels)
        {
            if (ReferenceEquals(label.Edge, hovered))
            {
                HoveredLabels.Add(label.Bounds);
            }
        }

        foreach (var label in Labels)
        {
            if (label.IsHidden
                || ReferenceEquals(label.Edge, hovered)
                || !label.Bounds.IntersectsWith(view.Visible)
                || IsUnderHoveredLabel(label.Bounds))
            {
                continue;
            }

            DrawLabel(canvas, label, font, label.Edge.IsHot ? Paints.HotLine : Paints.SecondaryText, view);
        }

        foreach (var label in Labels)
        {
            if (ReferenceEquals(label.Edge, hovered))
            {
                DrawLabel(canvas, label, font, Paints.HoverLine, view);
            }
        }
    }

    private void DrawLabel(SKCanvas canvas, EdgeLabel label, SKFont font, SKColor colour, GraphView view)
        => DrawText(canvas,
                    label.IsChanges ? label.Edge.Changes : label.Edge.Label,
                    font,
                    label.Bounds.Left,
                    GetBaseline(font, label.Bounds.Top, LabelHeight),
                    colour,
                    view);

    private bool IsUnderHoveredLabel(SKRect bounds)
    {
        foreach (var hovered in HoveredLabels)
        {
            if (hovered.IntersectsWith(bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawSelection(SKCanvas canvas, GraphView view)
    {
        var rect = new SKRect((float)Math.Min(PressPoint.X, DragPoint.X) * view.Scale,
                              (float)Math.Min(PressPoint.Y, DragPoint.Y) * view.Scale,
                              (float)Math.Max(PressPoint.X, DragPoint.X) * view.Scale,
                              (float)Math.Max(PressPoint.Y, DragPoint.Y) * view.Scale);

        FillRect(canvas, Paints.DragFill, rect.Left, rect.Top, rect.Right, rect.Bottom);

        Paints.Stroke.Color = Paints.DragStroke;

        Paints.Stroke.StrokeWidth = view.Thickness;

        canvas.DrawRect(rect, Paints.Stroke);
    }

    private void FillRect(SKCanvas canvas, SKColor colour, float left, float top, float right, float bottom)
    {
        if (right <= left || bottom <= top)
        {
            return;
        }

        Paints.Fill.Color = colour;

        canvas.DrawRect(new SKRect(left, top, right, bottom), Paints.Fill);
    }

    private static float GetBaseline(SKFont font, float top, float height)
    {
        var metrics = font.Metrics;

        return top + (height - metrics.Ascent - metrics.Descent) / 2;
    }

    private string Trim(string text, SKFont font, float width, bool isDetail)
    {
        if (text.Length == 0 || width <= 0)
        {
            return string.Empty;
        }

        var key = (text, font.Size, width, isDetail);

        if (Trimmed.TryGetValue(key, out var trimmed))
        {
            return trimmed;
        }

        trimmed = text;

        if (font.MeasureText(text) > width)
        {
            var low = 0;

            var high = text.Length;

            while (low < high)
            {
                var middle = (low + high + 1) / 2;

                if (font.MeasureText(text[..middle] + Ellipsis) <= width)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            trimmed = text[..low] + Ellipsis;
        }

        if (Trimmed.Count >= TrimmedLimit)
        {
            Trimmed.Clear();
        }

        Trimmed[key] = trimmed;

        return trimmed;
    }

    private NodeLayout GetLayout(MemoryReferenceGraphNode node, MemoryReferenceGraph graph, float zoom, bool isDetail)
    {
        var top = (float)node.Y + (isDetail ? BorderWidth + BandHeight + LifetimeHeight : 0);

        var bottom = (float)(node.Y + (isDetail ? graph.NodeHeight : node.Height));

        var centre = (top + bottom) / 2;

        var left = (float)node.X + BorderWidth + ContentMarginLeft;

        var right = (float)(node.X + graph.NodeWidth) - BorderWidth - ContentMarginRight;

        var referrers = node.ToggleReferrers is null
            ? SKRect.Empty
            : new SKRect(left, centre - ToggleSize / 2, left + ToggleSize, centre + ToggleSize / 2);

        var expand = node.Toggle is null
            ? SKRect.Empty
            : new SKRect(right - ToggleSize, centre - ToggleSize / 2, right, centre + ToggleSize / 2);

        var textLeft = node.ToggleReferrers is null ? left : left + ToggleSize + ColumnSpacing;

        var textRight = node.Toggle is null ? right : right - ToggleSize - ColumnSpacing;

        if (isDetail)
        {
            var nameTop = centre - (NameLineHeight + DetailLineHeight) / 2;

            return new NodeLayout(referrers, expand, textLeft, textRight, right, nameTop, NameLineHeight, NameSize);
        }

        var size = Math.Clamp(MinimumNameSize / zoom, NameSize, (float)graph.NodeHeight * NameShare);

        var height = size * NameLineRatio;

        return new NodeLayout(referrers, expand, textLeft, textRight, right, centre - height / 2, height, size);
    }

    private GraphHit HitTest(Point position)
    {
        if (Graph is not { } graph)
        {
            return default;
        }

        var point = Viewport.ToContent(position.X, position.Y);

        var isDetail = Viewport.Zoom >= DetailZoom;

        for (var index = graph.Nodes.Count - 1; index >= 0; index--)
        {
            var node = graph.Nodes[index];

            if (point.X < node.X || point.X > node.X + graph.NodeWidth || point.Y < node.Y || point.Y > node.Y + node.Height)
            {
                continue;
            }

            var layout = GetLayout(node, graph, Viewport.Zoom, isDetail);

            if (layout.Expand.Contains(point))
            {
                return new GraphHit(node, MemoryReferenceGraphPart.Expand, null);
            }

            if (layout.Referrers.Contains(point))
            {
                return new GraphHit(node, MemoryReferenceGraphPart.Referrers, null);
            }

            var font = Sized(Paints.NameFont, layout.NameSize);

            var width = font.MeasureText(Trim(node.Label, font, layout.TextRight - layout.TextLeft, false));

            var isName = point.X >= layout.TextLeft
                         && point.X <= layout.TextLeft + width
                         && point.Y >= layout.NameTop
                         && point.Y <= layout.NameTop + layout.NameHeight;

            return new GraphHit(node, isName ? MemoryReferenceGraphPart.Name : MemoryReferenceGraphPart.Node, null);
        }

        if (isDetail && HitTestLabel(point) is { } labelled)
        {
            return new GraphHit(null, MemoryReferenceGraphPart.Label, labelled);
        }

        return HitTestEdge(graph, point) is { } edge ? new GraphHit(null, MemoryReferenceGraphPart.Edge, edge) : default;
    }

    private MemoryReferenceGraphEdge? HitTestLabel(SKPoint point)
    {
        var hovered = HoverHit.Edge;

        MemoryReferenceGraphEdge? found = null;

        foreach (var label in Labels)
        {
            if (!label.Bounds.Contains(point))
            {
                continue;
            }

            if (ReferenceEquals(label.Edge, hovered))
            {
                return hovered;
            }

            if (!label.IsHidden)
            {
                found = label.Edge;
            }
        }

        return found;
    }

    private MemoryReferenceGraphEdge? HitTestEdge(MemoryReferenceGraph graph, SKPoint point)
    {
        var tolerance = EdgeHitTolerance / Viewport.Zoom;

        var nearest = (double)tolerance;

        MemoryReferenceGraphEdge? found = null;

        for (var index = 0; index < graph.Edges.Count; index++)
        {
            var bounds = EdgeBounds[index];

            if (point.X < bounds.Left - tolerance
                || point.X > bounds.Right + tolerance
                || point.Y < bounds.Top - tolerance
                || point.Y > bounds.Bottom + tolerance)
            {
                continue;
            }

            var distance = GetDistance(graph.Edges[index].Points, point);

            if (distance <= nearest)
            {
                nearest = distance;

                found = graph.Edges[index];
            }
        }

        return found;
    }

    private static double GetDistance(Point[] points, SKPoint point)
    {
        var nearest = double.MaxValue;

        for (var index = 1; index < points.Length; index++)
        {
            var from = points[index - 1];

            var to = points[index];

            var x = Math.Clamp(point.X, Math.Min(from.X, to.X), Math.Max(from.X, to.X));

            var y = Math.Clamp(point.Y, Math.Min(from.Y, to.Y), Math.Max(from.Y, to.Y));

            nearest = Math.Min(nearest, Math.Max(Math.Abs(point.X - x), Math.Abs(point.Y - y)));
        }

        return nearest;
    }

    private readonly record struct EdgeLabel(MemoryReferenceGraphEdge Edge, bool IsChanges, SKRect Bounds)
    {
        public bool IsHidden => Edge.IsCrossLink;
    }

    private readonly record struct NodeLayout(SKRect Referrers,
                                              SKRect Expand,
                                              float TextLeft,
                                              float TextRight,
                                              float Right,
                                              float NameTop,
                                              float NameHeight,
                                              float NameSize);

    private readonly record struct GraphView(float Scale, float Zoom, float OffsetX, float OffsetY, float Width, float Height)
    {
        public float Thickness => Math.Max(1, MathF.Round(Scale));

        public float HoverThickness => Math.Max(HoverWidth, MathF.Round(Scale * HoverWidth));

        public SKRect Visible => new(OffsetX / Zoom, OffsetY / Zoom, (OffsetX + Width) / Zoom, (OffsetY + Height) / Zoom);

        public float ToX(double x) => (float)((x * Zoom - OffsetX) * Scale);

        public float ToY(double y) => (float)((y * Zoom - OffsetY) * Scale);

        public float ToPixels(float length) => length * Zoom * Scale;

        public float SnapX(double x, float thickness) => Snap(ToX(x), thickness);

        public float SnapY(double y, float thickness) => Snap(ToY(y), thickness);

        public bool IsVisible(MemoryReferenceGraphNode node, MemoryReferenceGraph graph)
            => Visible.IntersectsWith(new SKRect((float)node.X,
                                                 (float)node.Y,
                                                 (float)(node.X + graph.NodeWidth),
                                                 (float)(node.Y + node.Height)));

        private static float Snap(float value, float thickness) => thickness % 2 == 1 ? MathF.Floor(value) + 0.5f : MathF.Round(value);
    }
}
