using System;
using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Timeline;
using SkiaSharp;
using Windows.Foundation;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Timeline.Operators;

internal sealed partial class TraceOperatorsPane
{
    private const float StageInset = 4f;

    private const float StageLabelHeight = 28f;

    private const float StageLabelPadding = 8f;

    private const float StageLabelFontSize = 12f;

    private const float StageCornerRadius = 3f;

    private const int StageShade = 56;

    private const int StageShadeStep = 12;

    private static readonly SKColor StageEdgeColour = new(255, 255, 255, 36);

    private static readonly SKColor StageHoverColour = new(255, 255, 255, 160);

    public IReadOnlyList<TraceStage> Stages { get; set; } = [];

    public TraceStage? GetStageAt(Point position)
    {
        if (!Contains(position))
        {
            return null;
        }

        TraceStage? found = null;

        foreach (var stage in Stages)
        {
            if (GetStageRect(stage, Host.Width, Host.Scale) is { } rect
                && rect.Contains((float)position.X, (float)position.Y)
                && (found is null || stage.Depth > found.Depth))
            {
                found = stage;
            }
        }

        return found;
    }

    private void DrawStages(SKCanvas canvas, int width, double scale)
    {
        foreach (var stage in Stages)
        {
            if (GetStageRect(stage, width, scale) is not { } rect)
            {
                continue;
            }

            Shape.SetRect(rect, StageCornerRadius, StageCornerRadius);

            Paints.Fill.Color = GetStageColour(stage);

            canvas.DrawRoundRect(Shape, Paints.Fill);

            Paints.Outline.Color = StageEdgeColour;

            Paints.Outline.StrokeWidth = 1f;

            canvas.DrawRoundRect(Shape, Paints.Outline);

            DrawStageLabel(canvas, stage, rect);
        }
    }

    private void DrawStageHover(SKCanvas canvas, double scale)
    {
        if (Hover.Stage is not { } stage || GetStageRect(stage, Host.Width, scale) is not { } rect)
        {
            return;
        }

        Paints.Outline.Color = StageHoverColour;

        Paints.Outline.StrokeWidth = 1f;

        canvas.DrawRoundRect(rect, StageCornerRadius, StageCornerRadius, Paints.Outline);
    }

    private void DrawStageLabel(SKCanvas canvas, TraceStage stage, SKRect rect)
    {
        var font = Paints.OperatorBoldFont;

        font.Size = StageLabelFontSize;

        var metrics = font.Metrics;

        var baseline = rect.Top + StageLabelPadding + metrics.CapHeight;

        var left = Math.Max(rect.Left, 0) + StageLabelPadding;

        if (baseline + metrics.Descent > rect.Bottom || left + font.MeasureText(stage.Name) > rect.Right - StageLabelPadding)
        {
            return;
        }

        Paints.Text.Color = OperatorLabelColour;

        canvas.DrawText(stage.Name, left, baseline, SKTextAlign.Left, font, Paints.Text);
    }

    private SKRect? GetStageRect(TraceStage stage, int width, double scale)
    {
        var start = TraceScale.GetValue(stage.Span.From, Axis);

        var end = TraceScale.GetValue(stage.Span.To, Axis);

        if (end < Host.ViewStart || start > Host.ViewEnd)
        {
            return null;
        }

        var top = Top + StageInset + stage.Depth * StageLabelHeight;

        var bottom = Top + Height - StageInset * (stage.Depth + 1);

        if (bottom - top < 1)
        {
            return null;
        }

        var left = (float)((start - Host.ViewStart) * scale);

        var right = Math.Max((float)((end - Host.ViewStart) * scale), left + TraceTimelineControl.NarrowSpan * 2);

        return new SKRect(Math.Max(left, -StageCornerRadius * 2), top, Math.Min(right, width + StageCornerRadius * 2), bottom);
    }

    private string GetStageToolTip(TraceStage stage)
        => string.Join(Environment.NewLine,
                       stage.Name,
                       stage.Node.Symbol,
                       $"Thread {stage.ThreadId}",
                       $"Instructions {stage.Span.Instructions:N0}",
                       GetPositionLabel(stage.Span.From.Position, stage.Span.To.Position));

    private static SKColor GetStageColour(TraceStage stage)
    {
        var shade = (byte)Math.Min(StageShade + StageShadeStep * stage.Depth, byte.MaxValue);

        return new SKColor(shade, shade, shade);
    }
}
