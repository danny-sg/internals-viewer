using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.RowBuffers;
using InternalsViewer.Query.FullTrace.Timeline;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

public sealed partial class TraceTimelineControl
{
    private const float OutputRowStripOffset = 1f;

    private const float OutputRowStripHeight = 6f;

    private const float OutputRowGap = 8f;

    private const float LineageJoinOffset = 1.5f;

    private const float OutputRowHitWidth = 4f;

    private const float LineageMarkWidth = 3f;

    private const float MinimumLineageMarkHeight = 3f;

    private const float LineagePanelGap = 12f;

    private const float LineagePanelMargin = 3f;

    private const float LineageTitleHeight = 14f;

    private const float LineageLaneOffset = 10f;

    private const float LineageLaneSpacing = 8f;

    private const float GapLineOffset = 1.5f;

    private const float LineageShadowOffset = 1f;

    private const float LineagePanelPadding = 8f;

    private const float LineagePanelMinimumWidth = 120f;

    private const float LineagePanelMaximumWidth = 360f;

    private const float LineageColumnHeight = 13f;

    private const float LineageBulletWidth = 3f;

    private const float LineageBulletGap = 6f;

    private const float LineageBadgeGap = 6f;

    private const float LineageBadgePadding = 4f;

    private const float LineageBadgeInset = 1.5f;

    private const float LineageBadgeRadius = 3f;

    private const float LineageWrapStep = 8f;

    private const float LineageValueGap = 8f;

    private const float LineageMaximumValueWidth = 160f;

    private const string ColumnSeparator = ",";

    private const string DictionaryBadge = "Dictionary";

    private const string TrimmedValueSuffix = "\u2026";

    private const string OutputRowTitle = "Output Row";

    private static readonly SKColor OutputRowColour = new(0xFF, 0xD5, 0x4F);

    private static readonly SKColor DictionaryBadgeColour = new(0x8C, 0xC8, 0xFF, 0x50);

    public static readonly DependencyProperty OutputColumnNamesProperty =
        DependencyProperty.Register(nameof(OutputColumnNames),
                                    typeof(IReadOnlyList<string>),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null));

    public IReadOnlyList<string>? OutputColumnNames
    {
        get => (IReadOnlyList<string>?)GetValue(OutputColumnNamesProperty);
        set => SetValue(OutputColumnNamesProperty, value);
    }

    public static readonly DependencyProperty OutputValueFormatterProperty =
        DependencyProperty.Register(nameof(OutputValueFormatter),
                                    typeof(Func<RowBuffer, RowBufferColumn, bool, string>),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(null, OnOutputValuesChanged));

    public Func<RowBuffer, RowBufferColumn, bool, string>? OutputValueFormatter
    {
        get => (Func<RowBuffer, RowBufferColumn, bool, string>?)GetValue(OutputValueFormatterProperty);
        set => SetValue(OutputValueFormatterProperty, value);
    }

    public static readonly DependencyProperty IsDecodedProperty =
        DependencyProperty.Register(nameof(IsDecoded),
                                    typeof(bool),
                                    typeof(TraceTimelineControl),
                                    new PropertyMetadata(true, OnOutputValuesChanged));

    public bool IsDecoded
    {
        get => (bool)GetValue(IsDecodedProperty);
        set => SetValue(IsDecodedProperty, value);
    }

    private TraceOutputRow? _hoverOutputRow;

    private LineageBlocks? _lineageBlocks;

    private TraceOutputRows? OutputRows => RowReturnMarks?.OutputRows is { Count: > 0 } rows ? rows : null;

    private bool ShowsOutputRows => ShowsOperators && OutputRows is not null;

    private float OutputRowsArea => ShowsOutputRows ? OutputRowStripHeight + OutputRowGap : 0;

    private float OutputRowsTop => OperatorsTop + OutputRowStripOffset;

    private float LineageJoinY => OutputRowsTop + OutputRowStripHeight + LineageJoinOffset;

    private TraceOutputRow? LineageRow
        => _hoverOutputRow ?? (_playhead is { } playhead ? OutputRows?.GetRow(_axis, playhead) : null);

    private bool IsLineageVisible => GetVisibleLineageRow((int)_overlay.ActualWidth) is not null;

    private int LineageColumnCapacity => Math.Max((int)(OperatorRowHeight / LineageColumnHeight), 1);

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

    private TraceOutputRow? GetVisibleLineageRow(int width)
    {
        if (!ShowsOutputRows || LineageRow is not { } row)
        {
            return null;
        }

        var x = GetCentre(row.Return, PixelsPerUnit(width));

        return x >= 0 && x <= width ? row : null;
    }

    private LineageLayout? GetLineageLayout(int width, double scale)
    {
        if (GetVisibleLineageRow(width) is not { } row)
        {
            return null;
        }

        var x = GetCentre(row.Return, scale);

        var steps = GetGetRowSteps(row);

        var root = GetTick(row.Return);

        var sources = GetValueSources(row);

        var columns = GetColumnBlocks(row);

        var panelWidth = GetPanelWidth(row, columns);

        var rightGap = GetLanesWidth(sources.Count) + LineagePanelGap;

        var leftGap = GetLanesWidth(steps.Count + (root is null ? 0 : 1)) + LineagePanelGap;

        var fitsRight = x + rightGap + panelWidth <= width;

        var fitsLeft = x - leftGap - panelWidth >= 0;

        var isRight = fitsRight || (!fitsLeft && width - x >= x);

        var left = Math.Clamp(isRight ? x + rightGap : x - leftGap - panelWidth, 0, Math.Max(width - panelWidth, 0));

        var panel = new SKRect(left,
                               OperatorsTop + OutputRowStripHeight + 1,
                               left + panelWidth,
                               OperatorsTop + OperatorsHeight - LineagePanelMargin);

        return new LineageLayout(row, x, root, steps, sources, columns, panel);
    }

    private List<LineageLine> GetLineageLines(LineageLayout layout, double scale)
    {
        var x = layout.X;

        var joinY = LineageJoinY;

        var lines = new List<LineageLine>();

        var rowLane = 0;

        if (layout.Root is { } root)
        {
            lines.Add(GetRowLine(x, root, x - GetLaneOffset(rowLane++), x, joinY));
        }

        foreach (var (step, tick) in layout.Steps)
        {
            lines.Add(GetRowLine(GetCentre(step.Return, scale), tick, x - GetLaneOffset(rowLane++), x, joinY));
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
                                          new SKPoint(laneX, joinY),
                                          new SKPoint(x + LineageMarkWidth / 2, joinY)
                                      ]));
        }

        return lines;
    }

    private LineageLine GetRowLine(float tickX, LineageTick tick, float laneX, float x, float joinY)
    {
        var gapY = GapAbove(tick.Row) - GapLineOffset;

        return new LineageLine(_paints.OutputRowLink,
                               [
                                   new SKPoint(tickX, tick.Top),
                                   new SKPoint(tickX, gapY),
                                   new SKPoint(laneX, gapY),
                                   new SKPoint(laneX, joinY),
                                   new SKPoint(x - LineageMarkWidth / 2, joinY)
                               ]);
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

        canvas.DrawRect(layout.X - LineageMarkWidth / 2,
                        OutputRowsTop,
                        LineageMarkWidth,
                        LineageJoinY - OutputRowsTop + LineageJoinOffset / 2,
                        _paints.Fill);

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

    private void DrawLineagePanel(SKCanvas canvas, TraceOutputRow row, List<(int Row, List<LineageColumn> Columns)> blocks, SKRect panel)
    {
        canvas.DrawRect(panel, _paints.LabelBackground);

        canvas.Save();

        canvas.ClipRect(panel);

        var textLeft = panel.Left + LineagePanelPadding;

        canvas.DrawText(GetTitle(row),
                        textLeft,
                        GetBaseline(_paints.HeaderBoldFont, panel.Top, LineageTitleHeight),
                        SKTextAlign.Left,
                        _paints.HeaderBoldFont,
                        _paints.Label);

        var nameLeft = textLeft + LineageBulletWidth + LineageBulletGap;

        var previousBottom = panel.Top + LineageTitleHeight;

        foreach (var (operatorRow, columns) in blocks)
        {
            var lines = GetColumnLines(columns, panel.Right - LineagePanelPadding - nameLeft);

            var blockHeight = lines.Count * LineageColumnHeight;

            var blockTop = Math.Max(OperatorRowTop(operatorRow) + (OperatorRowHeight - blockHeight) / 2, previousBottom);

            previousBottom = blockTop + blockHeight;

            _paints.Fill.Color = RowBufferMarkColour;

            canvas.DrawRect(textLeft, blockTop + 2, LineageBulletWidth, Math.Max(blockHeight - 4, 1), _paints.Fill);

            for (var line = 0; line < lines.Count; line++)
            {
                var lineTop = blockTop + line * LineageColumnHeight;

                foreach (var run in lines[line])
                {
                    DrawColumnRun(canvas, run, nameLeft + run.Left, lineTop);
                }
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

    private List<(int Row, List<LineageColumn> Columns)> GetColumnBlocks(TraceOutputRow row)
    {
        if (_lineageBlocks is { } cached && ReferenceEquals(cached.Row, row) && cached.IsDecoded == IsDecoded)
        {
            return cached.Blocks;
        }

        List<(int Row, List<LineageColumn> Columns)> blocks =
            [.. row.Sources
                   .Select(s => (Row: GetRowIndex(s.Operator), Columns: s.Columns.Select(GetLineageColumn).ToList()))
                   .Where(s => s.Row is not null && s.Columns.Count > 0)
                   .Select(s => (s.Row!.Value, s.Columns))
                   .OrderBy(s => s.Item1)];

        _lineageBlocks = new LineageBlocks(row, IsDecoded, blocks);

        return blocks;
    }

    private LineageColumn GetLineageColumn(TraceOutputColumn column)
        => new(GetColumnName(column.Ordinal), GetValueText(column.Row, column.Value), column.IsFromDictionary);

    private string? GetValueText(RowBuffer? row, RowBufferColumn? column)
        => row is not null && column is not null && OutputValueFormatter is { } formatter
            ? GetTrimmed(formatter(row, column, IsDecoded))
            : null;

    private string GetTrimmed(string text)
    {
        if (_paints.Font.MeasureText(text) <= LineageMaximumValueWidth)
        {
            return text;
        }

        var (low, high) = (0, text.Length - 1);

        while (low < high)
        {
            var middle = (low + high + 1) / 2;

            if (_paints.Font.MeasureText(text[..middle] + TrimmedValueSuffix) <= LineageMaximumValueWidth)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return text[..low] + TrimmedValueSuffix;
    }

    private void DrawColumnRun(SKCanvas canvas, LineageColumnRun run, float left, float lineTop)
    {
        var baseline = GetBaseline(_paints.Font, lineTop, LineageColumnHeight);

        canvas.DrawText(run.Column.Name, left, baseline, SKTextAlign.Left, _paints.Font, _paints.DimLabel);

        var right = left + _paints.Font.MeasureText(run.Column.Name);

        if (run.Column.Value is { } value)
        {
            canvas.DrawText(value, left + run.ValueLeft, baseline, SKTextAlign.Left, _paints.Font, _paints.Label);

            right = left + run.ValueLeft + _paints.Font.MeasureText(value);
        }

        if (run.Column.IsFromDictionary)
        {
            right = DrawBadge(canvas, DictionaryBadge, right + LineageBadgeGap, lineTop);
        }

        if (run.HasSeparator)
        {
            canvas.DrawText(ColumnSeparator, right, baseline, SKTextAlign.Left, _paints.Font, _paints.Label);
        }
    }

    private float DrawBadge(SKCanvas canvas, string text, float left, float lineTop)
    {
        var rect = new SKRect(left,
                              lineTop + LineageBadgeInset,
                              left + _paints.Font.MeasureText(text) + LineageBadgePadding * 2,
                              lineTop + LineageColumnHeight - LineageBadgeInset);

        _paints.Fill.Color = DictionaryBadgeColour;

        canvas.DrawRoundRect(rect, LineageBadgeRadius, LineageBadgeRadius, _paints.Fill);

        canvas.DrawText(text,
                        rect.Left + LineageBadgePadding,
                        GetBaseline(_paints.Font, lineTop, LineageColumnHeight),
                        SKTextAlign.Left,
                        _paints.Font,
                        _paints.Label);

        return rect.Right;
    }

    private float GetPanelWidth(TraceOutputRow row, List<(int Row, List<LineageColumn> Columns)> blocks)
    {
        var width = _paints.HeaderBoldFont.MeasureText(GetTitle(row));

        foreach (var (_, columns) in blocks)
        {
            width = Math.Max(width, LineageBulletWidth + LineageBulletGap + GetBlockWidth(columns));
        }

        return Math.Clamp(width + LineagePanelPadding * 2, LineagePanelMinimumWidth, LineagePanelMaximumWidth);
    }

    private float GetBlockWidth(List<LineageColumn> columns)
    {
        if (columns.Count <= LineageColumnCapacity)
        {
            var valueLeft = GetValueLeft(columns);

            return columns.Max(c => GetRunWidth(new LineageColumnRun(c, 0, valueLeft, false)));
        }

        var maximum = LineagePanelMaximumWidth - LineagePanelPadding * 2 - LineageBulletWidth - LineageBulletGap;

        var width = Math.Min(columns.Select((_, i) => GetRunWidth(GetWrappedRun(columns, i))).Max(), maximum);

        while (width < maximum && GetColumnLines(columns, width).Count > LineageColumnCapacity)
        {
            width = Math.Min(width + LineageWrapStep, maximum);
        }

        return width;
    }

    private List<List<LineageColumnRun>> GetColumnLines(List<LineageColumn> columns, float width)
    {
        if (columns.Count <= LineageColumnCapacity)
        {
            var valueLeft = GetValueLeft(columns);

            return [.. columns.Select(c => new List<LineageColumnRun> { new(c, 0, valueLeft, false) })];
        }

        var space = _paints.Font.MeasureText(" ");

        var lines = new List<List<LineageColumnRun>>();

        var line = new List<LineageColumnRun>();

        var left = 0f;

        for (var index = 0; index < columns.Count; index++)
        {
            var run = GetWrappedRun(columns, index);

            var runWidth = GetRunWidth(run);

            if (line.Count > 0 && left + space + runWidth > width)
            {
                lines.Add(line);

                line = [];

                left = 0;
            }
            else if (line.Count > 0)
            {
                left += space;
            }

            line.Add(run with { Left = left });

            left += runWidth;
        }

        lines.Add(line);

        return lines;
    }

    private LineageColumnRun GetWrappedRun(List<LineageColumn> columns, int index)
        => new(columns[index],
               0,
               _paints.Font.MeasureText(columns[index].Name) + _paints.Font.MeasureText(" "),
               index < columns.Count - 1);

    private float GetValueLeft(List<LineageColumn> columns) => columns.Max(c => _paints.Font.MeasureText(c.Name)) + LineageValueGap;

    private float GetRunWidth(LineageColumnRun run)
    {
        var width = run.Column.Value is { } value
            ? run.ValueLeft + _paints.Font.MeasureText(value)
            : _paints.Font.MeasureText(run.Column.Name);

        if (run.Column.IsFromDictionary)
        {
            width += LineageBadgeGap + _paints.Font.MeasureText(DictionaryBadge) + LineageBadgePadding * 2;
        }

        return run.HasSeparator ? width + _paints.Font.MeasureText(ColumnSeparator) : width;
    }

    private string GetColumnName(int ordinal)
        => OutputColumnNames is { } names && ordinal < names.Count ? names[ordinal] : $"Column {ordinal + 1}";

    private static string GetTitle(TraceOutputRow row) => $"{OutputRowTitle} {row.Ordinal:N0}";

    private static float GetLaneOffset(int lane) => LineageLaneOffset + lane * LineageLaneSpacing;

    private static float GetLanesWidth(int lanes) => lanes == 0 ? 0 : GetLaneOffset(lanes - 1);

    private static float GetBaseline(SKFont font, float top, float height)
    {
        var metrics = font.Metrics;

        return top + (height - (metrics.Descent - metrics.Ascent)) / 2 - metrics.Ascent;
    }

    private bool IsOnOutputRows(Point position)
        => ShowsOutputRows && position.Y >= OperatorsTop && position.Y < OutputRowsTop + OutputRowStripHeight + OutputRowGap / 2;

    private TraceOutputRow? GetOutputRow(Point position)
    {
        if (!IsOnOutputRows(position) || OutputRows is not { } rows)
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
            : OperatorRowTop(row) + OperatorRowHeight + (OperatorsPadding + OperatorsBottomGap) / 2;

    private float GapAbove(int row)
        => row == 0
            ? (LineageJoinY + OperatorRowTop(0)) / 2 + GapLineOffset
            : OperatorRowTop(row) - OperatorRowGap / 2;

    private static void OnOutputValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TraceTimelineControl)d;

        control._lineageBlocks = null;

        control.Redraw();
    }

    private readonly record struct LineageTick(int Row, float Top, float Height);

    private readonly record struct LineageColumn(string Name, string? Value, bool IsFromDictionary);

    private readonly record struct LineageColumnRun(LineageColumn Column, float Left, float ValueLeft, bool HasSeparator);

    private sealed record LineageBlocks(TraceOutputRow Row, bool IsDecoded, List<(int Row, List<LineageColumn> Columns)> Blocks);

    private sealed record LineageLine(SKPaint Paint, SKPoint[] Points);

    private sealed record LineageLayout(TraceOutputRow Row,
                                        float X,
                                        LineageTick? Root,
                                        List<(TraceOutputStep Step, LineageTick Tick)> Steps,
                                        List<(TraceRowBufferMark RowBuffer, int Row)> Sources,
                                        List<(int Row, List<LineageColumn> Columns)> Columns,
                                        SKRect Panel);
}
