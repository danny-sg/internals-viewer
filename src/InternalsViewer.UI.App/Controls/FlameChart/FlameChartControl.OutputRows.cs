using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.FlameChart;

public sealed partial class FlameChartControl
{
    private const float OutputRowStripOffset = 1f;

    private const float OutputRowStripHeight = 6f;

    private const float OutputRowGap = 4f;

    private const float OutputRowHitWidth = 4f;

    private const float LineageMarkWidth = 3f;

    private const float MinimumLineageMarkHeight = 3f;

    private const float LineagePanelGap = 12f;

    private const float LineageLaneOffset = 10f;

    private const float LineageLaneSpacing = 8f;

    private const float GapLineOffset = 1.5f;

    private const float LineageShadowOffset = 1f;

    private const float LineagePanelPadding = 8f;

    private const float LineagePanelMinimumWidth = 120f;

    private const float LineagePanelMaximumWidth = 280f;

    private const float LineageColumnHeight = 13f;

    private const float LineageBulletWidth = 3f;

    private const float LineageBulletGap = 6f;

    private const string OutputRowTitle = "Output Row";

    private static readonly SKColor OutputRowColour = new(0xFF, 0xD5, 0x4F);

    public static readonly DependencyProperty OutputColumnNamesProperty =
        DependencyProperty.Register(nameof(OutputColumnNames),
                                    typeof(IReadOnlyList<string>),
                                    typeof(FlameChartControl),
                                    new PropertyMetadata(null));

    public IReadOnlyList<string>? OutputColumnNames
    {
        get => (IReadOnlyList<string>?)GetValue(OutputColumnNamesProperty);
        set => SetValue(OutputColumnNamesProperty, value);
    }

    private TraceOutputRow? _hoverOutputRow;

    private TraceOutputRows? OutputRows => RowReturnMarks?.OutputRows is { Count: > 0 } rows ? rows : null;

    private bool ShowsOutputRows => ShowsOperators && OutputRows is not null;

    private float OutputRowsArea => ShowsOutputRows ? OutputRowStripHeight + OutputRowGap : 0;

    private float OutputRowsTop => OperatorsTop + OutputRowStripOffset;

    private TraceOutputRow? LineageRow
        => _hoverOutputRow ?? (_playhead is { } playhead ? OutputRows?.GetRow(_axis, playhead) : null);

    private void DrawOutputRows(SKCanvas canvas, double scale)
    {
        if (ShowsOutputRows && OutputRows is { } rows)
        {
            DrawMarks(canvas, rows.GetMarks(_axis), OutputRowsTop, OutputRowStripHeight, scale, OutputRowColour);
        }
    }

    private void DrawOutputRowLineage(SKCanvas canvas, int width, double scale)
    {
        if (GetLineageLayout(width, scale) is not { } layout)
        {
            return;
        }

        DrawLines(canvas, GetLineageLines(layout, scale));

        DrawLineageMarks(canvas, layout, scale);

        DrawLineagePanel(canvas, layout.Row, layout.Columns, layout.Panel);
    }

    private LineageLayout? GetLineageLayout(int width, double scale)
    {
        if (!ShowsOutputRows || LineageRow is not { } row)
        {
            return null;
        }

        var x = GetCentre(row.Return, scale);

        var steps = GetGetRowSteps(row);

        var sources = GetValueSources(row);

        var columns = GetColumnBlocks(row);

        var panelWidth = GetPanelWidth(row, columns);

        var rightGap = GetLanesWidth(sources.Count) + LineagePanelGap;

        var leftGap = GetLanesWidth(steps.Count) + LineagePanelGap;

        var fitsRight = x + rightGap + panelWidth <= width;

        var fitsLeft = x - leftGap - panelWidth >= 0;

        var isRight = fitsRight || (!fitsLeft && width - x >= x);

        var left = Math.Clamp(isRight ? x + rightGap : x - leftGap - panelWidth, 0, Math.Max(width - panelWidth, 0));

        var panel = new SKRect(left, OperatorsTop, left + panelWidth, OperatorsTop + OperatorsHeight);

        return new LineageLayout(row, x, GetTick(row.Return), steps, sources, columns, panel);
    }

    private List<LineageLine> GetLineageLines(LineageLayout layout, double scale)
    {
        var x = layout.X;

        var stripMiddle = OutputRowsTop + OutputRowStripHeight / 2;

        var lines = new List<LineageLine>();

        if (layout.Root is { } root)
        {
            lines.Add(new LineageLine(_paints.OutputRowLink,
                                      [new SKPoint(x, OutputRowsTop + OutputRowStripHeight), new SKPoint(x, root.Top)]));
        }

        for (var lane = 0; lane < layout.Steps.Count; lane++)
        {
            var (step, tick) = layout.Steps[lane];

            var tickX = GetCentre(step.Return, scale);

            var laneX = x - GetLaneOffset(lane);

            var gapY = GapAbove(tick.Row) - GapLineOffset;

            lines.Add(new LineageLine(_paints.OutputRowLink,
                                      [
                                          new SKPoint(tickX, tick.Top),
                                          new SKPoint(tickX, gapY),
                                          new SKPoint(laneX, gapY),
                                          new SKPoint(laneX, stripMiddle),
                                          new SKPoint(x - LineageMarkWidth / 2, stripMiddle)
                                      ]));
        }

        for (var lane = 0; lane < layout.Sources.Count; lane++)
        {
            var (rowBuffer, row) = layout.Sources[lane];

            var markX = GetCentre(rowBuffer, scale);

            var laneX = x + GetLaneOffset(lane);

            var (top, height) = GetMarkBand(row);

            var gapY = GapBelow(row);

            lines.Add(new LineageLine(_paints.OutputValueLink,
                                      [
                                          new SKPoint(markX, top + height),
                                          new SKPoint(markX, gapY),
                                          new SKPoint(laneX, gapY),
                                          new SKPoint(laneX, stripMiddle),
                                          new SKPoint(x + LineageMarkWidth / 2, stripMiddle)
                                      ]));
        }

        return lines;
    }

    private void DrawLines(SKCanvas canvas, List<LineageLine> lines)
    {
        foreach (var line in lines)
        {
            AddPolyline(line.Points);
        }

        using (var shadow = _pathBuilder.Detach())
        {
            canvas.Save();

            canvas.Translate(LineageShadowOffset, LineageShadowOffset);

            canvas.DrawPath(shadow, _paints.LineageShadow);

            canvas.Restore();
        }

        foreach (var line in lines)
        {
            AddPolyline(line.Points);

            using var path = _pathBuilder.Detach();

            canvas.DrawPath(path, line.Paint);
        }
    }

    private void DrawLineageMarks(SKCanvas canvas, LineageLayout layout, double scale)
    {
        _paints.Fill.Color = OutputRowColour;

        canvas.DrawRect(layout.X - LineageMarkWidth / 2, OutputRowsTop, LineageMarkWidth, OutputRowStripHeight, _paints.Fill);

        if (layout.Root is { } root)
        {
            DrawTick(canvas, layout.X, root);
        }

        foreach (var (step, tick) in layout.Steps)
        {
            DrawTick(canvas, GetCentre(step.Return, scale), tick);
        }

        _paints.Fill.Color = RowBufferMarkColour;

        foreach (var (rowBuffer, row) in layout.Sources)
        {
            var (top, height) = GetMarkBand(row);

            canvas.DrawRect(GetCentre(rowBuffer, scale) - LineageMarkWidth / 2, top, LineageMarkWidth, height, _paints.Fill);
        }
    }

    private void DrawLineagePanel(SKCanvas canvas, TraceOutputRow row, List<(int Row, List<string> Names)> columns, SKRect panel)
    {
        canvas.DrawRect(panel, _paints.LabelBackground);

        canvas.Save();

        canvas.ClipRect(panel);

        var textLeft = panel.Left + LineagePanelPadding;

        canvas.DrawText(GetTitle(row),
                        textLeft,
                        GetBaseline(_paints.HeaderBoldFont, OperatorsTop, OperatorRowTop(0) - OperatorsTop),
                        SKTextAlign.Left,
                        _paints.HeaderBoldFont,
                        _paints.Label);

        foreach (var (operatorRow, names) in columns)
        {
            var lines = FitLines(names, Math.Max((int)(OperatorRowHeight / LineageColumnHeight), 1));

            var blockHeight = lines.Count * LineageColumnHeight;

            var blockTop = OperatorRowTop(operatorRow) + Math.Max((OperatorRowHeight - blockHeight) / 2, 0);

            _paints.Fill.Color = RowBufferMarkColour;

            canvas.DrawRect(textLeft, blockTop + 2, LineageBulletWidth, Math.Max(blockHeight - 4, 1), _paints.Fill);

            for (var line = 0; line < lines.Count; line++)
            {
                canvas.DrawText(lines[line],
                                textLeft + LineageBulletWidth + LineageBulletGap,
                                GetBaseline(_paints.Font, blockTop + line * LineageColumnHeight, LineageColumnHeight),
                                SKTextAlign.Left,
                                _paints.Font,
                                _paints.Label);
            }
        }

        canvas.Restore();
    }

    private void DrawTick(SKCanvas canvas, float x, LineageTick tick)
    {
        _paints.Fill.Color = OutputRowColour;

        canvas.DrawRect(x - LineageMarkWidth / 2, tick.Top, LineageMarkWidth, tick.Height, _paints.Fill);
    }

    private void AddPolyline(SKPoint[] points)
    {
        _pathBuilder.MoveTo(points[0].X, points[0].Y);

        for (var index = 1; index < points.Length; index++)
        {
            _pathBuilder.LineTo(points[index].X, points[index].Y);
        }
    }

    private List<(TraceOutputStep Step, LineageTick Tick)> GetGetRowSteps(TraceOutputRow row)
        => [.. row.Steps
                  .Select(s => (Step: s, Tick: GetTick(s.Return)))
                  .Where(s => s.Tick is not null)
                  .Select(s => (s.Step, s.Tick!.Value))
                  .OrderBy(s => s.Item2.Row)];

    private List<(TraceRowBufferMark RowBuffer, int Row)> GetValueSources(TraceOutputRow row)
        => [.. row.Sources
                  .Select(s => (s.RowBuffer, Row: GetRowIndex(s.Operator)))
                  .Where(s => s.RowBuffer is not null && s.Row is not null)
                  .Select(s => (s.RowBuffer!, s.Row!.Value))
                  .OrderBy(s => s.Item2)];

    private List<(int Row, List<string> Names)> GetColumnBlocks(TraceOutputRow row)
        => [.. row.Sources
                  .Select(s => (Row: GetRowIndex(s.Operator), Names: s.Columns.Select(GetColumnName).ToList()))
                  .Where(s => s.Row is not null && s.Names.Count > 0)
                  .Select(s => (s.Row!.Value, s.Names))
                  .OrderBy(s => s.Item1)];

    private float GetPanelWidth(TraceOutputRow row, List<(int Row, List<string> Names)> columns)
    {
        var width = _paints.HeaderBoldFont.MeasureText(GetTitle(row));

        foreach (var name in columns.SelectMany(c => c.Names))
        {
            width = Math.Max(width, LineageBulletWidth + LineageBulletGap + _paints.Font.MeasureText(name));
        }

        return Math.Clamp(width + LineagePanelPadding * 2, LineagePanelMinimumWidth, LineagePanelMaximumWidth);
    }

    private string GetColumnName(int ordinal)
        => OutputColumnNames is { } names && ordinal < names.Count ? names[ordinal] : $"Column {ordinal + 1}";

    private static List<string> FitLines(List<string> names, int capacity)
    {
        if (names.Count <= capacity)
        {
            return names;
        }

        if (capacity == 1)
        {
            return [$"{names[0]} +{names.Count - 1} More"];
        }

        return [.. names.Take(capacity - 1), $"+{names.Count - capacity + 1} More"];
    }

    private static string GetTitle(TraceOutputRow row) => $"{OutputRowTitle} {row.Ordinal:N0}";

    private static float GetLaneOffset(int lane) => LineageLaneOffset + lane * LineageLaneSpacing;

    private static float GetLanesWidth(int lanes) => lanes == 0 ? 0 : GetLaneOffset(lanes - 1);

    private static float GetBaseline(SKFont font, float top, float height)
    {
        var metrics = font.Metrics;

        return top + (height - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;
    }

    private TraceOutputRow? GetOutputRow(Point position)
    {
        if (!ShowsOutputRows
            || OutputRows is not { } rows
            || position.Y < OperatorsTop
            || position.Y >= OutputRowsTop + OutputRowStripHeight + OutputRowGap / 2)
        {
            return null;
        }

        var scale = PixelsPerUnit((int)_overlay.ActualWidth);

        if (rows.GetNearest(_axis, _viewStart + position.X / scale) is not { } row)
        {
            return null;
        }

        return Math.Abs(GetCentre(row.Return, scale) - position.X) <= OutputRowHitWidth ? row : null;
    }

    private void MovePlayheadToOutputRow(TraceOutputRow row)
    {
        StopPlay();

        StopTransition();

        PlacePlayhead(row.Return.GetValue(_axis));
    }

    private float GetCentre(TraceRowReturn rowReturn, double scale)
        => (float)((rowReturn.GetValue(_axis) - _viewStart) * scale) + RowBufferMarkWidth / 2;

    private float GetCentre(TraceRowBufferMark mark, double scale)
        => (float)((mark.GetValue(_axis) - _viewStart) * scale) + RowBufferMarkWidth / 2;

    private int? GetRowIndex(ExecutionOperatorEvent operatorEvent)
    {
        for (var row = 0; row < _operatorRows.Length; row++)
        {
            if (ReferenceEquals(_operatorRows[row].Operator, operatorEvent))
            {
                return row;
            }
        }

        return null;
    }

    private LineageTick? GetTick(TraceRowReturn rowReturn)
    {
        if (GetRowIndex(rowReturn.Operator) is not { } row)
        {
            return null;
        }

        var lifetimes = _operatorRows[row].Lifetimes;

        var track = Array.FindIndex(lifetimes, l => l.Thread == rowReturn.Thread);

        if (track < 0)
        {
            return new LineageTick(row, OperatorRowTop(row), MinimumLineageMarkHeight);
        }

        var (top, height) = TrackBounds(row, track);

        return new LineageTick(row, top, Math.Max(ReturnStripHeightOf(new SKRect(0, top, 0, top + height)), MinimumLineageMarkHeight));
    }

    private (float Top, float Height) GetMarkBand(int row)
    {
        var height = Math.Max(MarkBandHeightOf(row), MinimumLineageMarkHeight);

        return (OperatorRowTop(row) + OperatorRowHeight - height, height);
    }

    private float GapBelow(int row)
        => row < _operatorRows.Length - 1
            ? OperatorRowTop(row) + OperatorRowHeight + OperatorRowGap / 2 + GapLineOffset
            : OperatorRowTop(row) + OperatorRowHeight + OperatorsPadding / 2;

    private float GapAbove(int row)
        => row == 0
            ? (OutputRowsTop + OutputRowStripHeight + OperatorRowTop(0)) / 2
            : OperatorRowTop(row) - OperatorRowGap / 2;

    private readonly record struct LineageTick(int Row, float Top, float Height);

    private sealed record LineageLine(SKPaint Paint, SKPoint[] Points);

    private sealed record LineageLayout(TraceOutputRow Row,
                                        float X,
                                        LineageTick? Root,
                                        List<(TraceOutputStep Step, LineageTick Tick)> Steps,
                                        List<(TraceRowBufferMark RowBuffer, int Row)> Sources,
                                        List<(int Row, List<string> Names)> Columns,
                                        SKRect Panel);
}
