using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.FullTrace.Timeline;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Trace;

internal sealed partial class TraceOperatorsPane
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

    private const float LineageWrapStep = 8f;

    private const string ColumnSeparator = ",";

    private const string OutputRowTitle = "Output Row";

    private static readonly SKColor OutputRowColour = new(0xFF, 0xD5, 0x4F);

    private static readonly SKColor LineageSourceColour = new(0xFF, 0x99, 0xA4);

    public IReadOnlyList<string>? OutputColumnNames { get; set; }

    public TraceOutputRows? OutputRows => RowReturnMarks?.OutputRows is { Count: > 0 } rows ? rows : null;

    public bool IsLineageVisible => GetVisibleLineageRow(Host.Width) is not null;

    private LineageBlocks? CachedBlocks { get; set; }

    private bool ShowsOutputRows => HasRows && OutputRows is not null;

    private float OutputRowsArea => ShowsOutputRows ? OutputRowStripHeight + OutputRowGap : 0;

    private float OutputRowsTop => Top + OutputRowStripOffset;

    private float LineageJoinY => OutputRowsTop + OutputRowStripHeight + LineageJoinOffset;

    private TraceOutputRow? LineageRow
        => Hover.OutputRow ?? (Host.Playhead is { } playhead ? OutputRows?.GetRow(Axis, playhead) : null);

    private int LineageColumnCapacity => Math.Max((int)(RowHeight / LineageColumnHeight), 1);

    private void DrawOutputRows(SKCanvas canvas, double scale)
    {
        if (ShowsOutputRows && OutputRows is { } rows)
        {
            DrawMarks(canvas, rows.GetMarks(Axis), OutputRowsTop, OutputRowStripHeight, scale, OutputRowColour);
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

        var x = GetCentre(row.Return, GetScale(width));

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

        var columns = GetColumnBlocks();

        var panelWidth = GetPanelWidth(row, columns);

        var rightGap = LineagePanelGap;

        var leftGap = GetLanesWidth(steps.Count + (root is null ? 0 : 1)) + LineagePanelGap;

        var fitsRight = x + rightGap + panelWidth <= width;

        var fitsLeft = x - leftGap - panelWidth >= 0;

        var isRight = fitsRight || (!fitsLeft && width - x >= x);

        var left = Math.Clamp(isRight ? x + rightGap : x - leftGap - panelWidth, 0, Math.Max(width - panelWidth, 0));

        var panel = new SKRect(left,
                               Top + OutputRowStripHeight + 1,
                               left + panelWidth,
                               Top + Height - LineagePanelMargin);

        return new LineageLayout(row, x, root, steps, columns, panel);
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

        return lines;
    }

    private LineageLine GetRowLine(float tickX, LineageTick tick, float laneX, float x, float joinY)
    {
        var gapY = GapAbove(tick.Row) - GapLineOffset;

        return new LineageLine(Paints.OutputRowLink,
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

        using (var shadow = Host.PathBuilder.Detach())
        {
            canvas.Save();

            canvas.Translate(LineageShadowOffset, LineageShadowOffset);

            canvas.DrawPath(shadow, Paints.LineageShadow);

            canvas.Restore();
        }

        foreach (var line in lines)
        {
            AddPolyline(line.Points);

            using var path = Host.PathBuilder.Detach();

            canvas.DrawPath(path, line.Paint);
        }
    }

    private void DrawLineageMarks(SKCanvas canvas, LineageLayout layout, double scale)
    {
        Paints.Fill.Color = OutputRowColour;

        canvas.DrawRect(layout.X - LineageMarkWidth / 2,
                        OutputRowsTop,
                        LineageMarkWidth,
                        LineageJoinY - OutputRowsTop + LineageJoinOffset / 2,
                        Paints.Fill);

        if (layout.Root is { } root)
        {
            DrawTick(canvas, layout.X, root);
        }

        foreach (var (step, tick) in layout.Steps)
        {
            DrawTick(canvas, GetCentre(step.Return, scale), tick);
        }
    }

    private void DrawLineagePanel(SKCanvas canvas, TraceOutputRow row, List<(int Row, List<string> Columns)> blocks, SKRect panel)
    {
        canvas.DrawRect(panel, Paints.LabelBackground);

        canvas.Save();

        canvas.ClipRect(panel);

        var textLeft = panel.Left + LineagePanelPadding;

        canvas.DrawText(GetTitle(row),
                        textLeft,
                        GetBaseline(Paints.HeaderBoldFont, panel.Top, LineageTitleHeight),
                        SKTextAlign.Left,
                        Paints.HeaderBoldFont,
                        Paints.Label);

        var nameLeft = textLeft + LineageBulletWidth + LineageBulletGap;

        var previousBottom = panel.Top + LineageTitleHeight;

        foreach (var (operatorRow, columns) in blocks)
        {
            var lines = GetColumnLines(columns, panel.Right - LineagePanelPadding - nameLeft);

            var blockHeight = lines.Count * LineageColumnHeight;

            var blockTop = Math.Max(GetRowTop(operatorRow) + (RowHeight - blockHeight) / 2, previousBottom);

            previousBottom = blockTop + blockHeight;

            Paints.Fill.Color = LineageSourceColour;

            canvas.DrawRect(textLeft, blockTop + 2, LineageBulletWidth, Math.Max(blockHeight - 4, 1), Paints.Fill);

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
        Paints.Fill.Color = OutputRowColour;

        canvas.DrawRect(x - LineageMarkWidth / 2, tick.Top, LineageMarkWidth, tick.Height, Paints.Fill);
    }

    private void AddPolyline(SKPoint[] points)
    {
        Host.PathBuilder.MoveTo(points[0].X, points[0].Y);

        for (var index = 1; index < points.Length; index++)
        {
            Host.PathBuilder.LineTo(points[index].X, points[index].Y);
        }
    }

    private List<(TraceOutputStep Step, LineageTick Tick)> GetGetRowSteps(TraceOutputRow row)
        => [.. row.Steps
                  .Select(s => (Step: s, Tick: GetTick(s.Return)))
                  .Where(s => s.Tick is not null)
                  .Select(s => (s.Step, s.Tick!.Value))
                  .OrderBy(s => s.Item2.Row)];

    private List<(int Row, List<string> Columns)> GetColumnBlocks()
    {
        if (OutputRows is not { } rows)
        {
            return [];
        }

        if (CachedBlocks is { } cached
            && ReferenceEquals(cached.Sources, rows.Sources)
            && ReferenceEquals(cached.Names, OutputColumnNames)
            && ReferenceEquals(cached.OperatorRows, Rows))
        {
            return cached.Blocks;
        }

        List<(int Row, List<string> Columns)> blocks =
            [.. rows.Sources
                    .Select(s => (Row: GetRowIndex(s.Operator), Columns: GetColumnNames(s)))
                    .Where(s => s.Row is not null && s.Columns.Count > 0)
                    .Select(s => (s.Row!.Value, s.Columns))
                    .OrderBy(s => s.Item1)];

        CachedBlocks = new LineageBlocks(rows.Sources, OutputColumnNames, Rows, blocks);

        return blocks;
    }

    private List<string> GetColumnNames(TraceOutputRowSource source)
        => source.IsWholeRow
            ? [.. Enumerable.Range(0, OutputColumnNames?.Count ?? 0).Select(GetColumnName)]
            : [.. source.Columns.Select(GetColumnName)];

    private void DrawColumnRun(SKCanvas canvas, LineageColumnRun run, float left, float lineTop)
    {
        var baseline = GetBaseline(Paints.Font, lineTop, LineageColumnHeight);

        canvas.DrawText(run.Name, left, baseline, SKTextAlign.Left, Paints.Font, Paints.Label);

        if (run.HasSeparator)
        {
            canvas.DrawText(ColumnSeparator,
                            left + Paints.Font.MeasureText(run.Name),
                            baseline,
                            SKTextAlign.Left,
                            Paints.Font,
                            Paints.Label);
        }
    }

    private float GetPanelWidth(TraceOutputRow row, List<(int Row, List<string> Columns)> blocks)
    {
        var width = Paints.HeaderBoldFont.MeasureText(GetTitle(row));

        foreach (var (_, columns) in blocks)
        {
            width = Math.Max(width, LineageBulletWidth + LineageBulletGap + GetBlockWidth(columns));
        }

        return Math.Clamp(width + LineagePanelPadding * 2, LineagePanelMinimumWidth, LineagePanelMaximumWidth);
    }

    private float GetBlockWidth(List<string> columns)
    {
        if (columns.Count <= LineageColumnCapacity)
        {
            return columns.Max(c => Paints.Font.MeasureText(c));
        }

        var maximum = LineagePanelMaximumWidth - LineagePanelPadding * 2 - LineageBulletWidth - LineageBulletGap;

        var width = Math.Min(columns.Select((_, i) => GetRunWidth(GetWrappedRun(columns, i))).Max(), maximum);

        while (width < maximum && GetColumnLines(columns, width).Count > LineageColumnCapacity)
        {
            width = Math.Min(width + LineageWrapStep, maximum);
        }

        return width;
    }

    private List<List<LineageColumnRun>> GetColumnLines(List<string> columns, float width)
    {
        if (columns.Count <= LineageColumnCapacity)
        {
            return [.. columns.Select(c => new List<LineageColumnRun> { new(c, 0, false) })];
        }

        var space = Paints.Font.MeasureText(" ");

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

    private static LineageColumnRun GetWrappedRun(List<string> columns, int index) => new(columns[index], 0, index < columns.Count - 1);

    private float GetRunWidth(LineageColumnRun run)
    {
        var width = Paints.Font.MeasureText(run.Name);

        return run.HasSeparator ? width + Paints.Font.MeasureText(ColumnSeparator) : width;
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

    public bool IsOnOutputRows(Point position)
        => ShowsOutputRows && position.Y >= Top && position.Y < OutputRowsTop + OutputRowStripHeight + OutputRowGap / 2;

    public TraceOutputRow? GetOutputRow(Point position)
    {
        if (!IsOnOutputRows(position) || OutputRows is not { } rows)
        {
            return null;
        }

        var scale = Host.Scale;

        if (rows.GetNearest(Axis, Host.ViewStart + position.X / scale) is not { } row)
        {
            return null;
        }

        return Math.Abs(GetCentre(row.Return, scale) - position.X) <= OutputRowHitWidth ? row : null;
    }

    private double GetScale(int width) => width / Math.Max(Host.ViewEnd - Host.ViewStart, double.Epsilon);

    private float GetCentre(TraceRowReturn rowReturn, double scale)
        => (float)((rowReturn.GetValue(Axis) - Host.ViewStart) * scale) + ReturnMarkWidth / 2;

    private LineageTick? GetTick(TraceRowReturn rowReturn)
    {
        if (GetRowIndex(rowReturn.Operator) is not { } row)
        {
            return null;
        }

        var lifetimes = Rows[row].Lifetimes;

        var track = Array.FindIndex(lifetimes, l => l.Thread == rowReturn.Thread);

        if (track < 0)
        {
            return new LineageTick(row, GetRowTop(row), MinimumLineageMarkHeight);
        }

        var (top, height) = GetTrackBounds(row, track);

        return new LineageTick(row, top, Math.Max(GetReturnStripHeight(new SKRect(0, top, 0, top + height)), MinimumLineageMarkHeight));
    }

    private float GapAbove(int row)
        => row == 0
            ? (LineageJoinY + GetRowTop(0)) / 2 + GapLineOffset
            : GetRowTop(row) - OperatorRowGap / 2;

    private readonly record struct LineageTick(int Row, float Top, float Height);

    private readonly record struct LineageColumnRun(string Name, float Left, bool HasSeparator);

    private sealed record LineageBlocks(IReadOnlyList<TraceOutputRowSource> Sources,
                                        IReadOnlyList<string>? Names,
                                        TraceOperatorRow[] OperatorRows,
                                        List<(int Row, List<string> Columns)> Blocks);

    private sealed record LineageLine(SKPaint Paint, SKPoint[] Points);

    private sealed record LineageLayout(TraceOutputRow Row,
                                        float X,
                                        LineageTick? Root,
                                        List<(TraceOutputStep Step, LineageTick Tick)> Steps,
                                        List<(int Row, List<string> Columns)> Columns,
                                        SKRect Panel);
}
