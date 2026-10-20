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
    private const string EvaluationsTitle = "Evaluations";

    private static readonly EvaluationStyle[] EvaluationStyles =
    [
        new(TraceEvaluationResult.False, new SKColor(0xF0, 0x48, 0x48), "False"),
        new(TraceEvaluationResult.Unknown, new SKColor(0x9E, 0x9E, 0x9E), "NULL"),
        new(TraceEvaluationResult.True, new SKColor(0x5C, 0xD6, 0x6B), "True")
    ];

    public TraceEvaluations? Evaluations { get; set; }

    public bool ShowEvaluations { get; set; } = true;

    public TraceEvaluation? GetEvaluationAt(Point position)
        => GetStripAt<TraceEvaluation>(position, GetEvaluations, GetEvaluationLevel) is { } strip
               ? GetNearestMark(strip.Marks, position)
               : null;

    private TraceOperatorHit? GetEvaluationStripAt(Point position)
        => GetStripAt<TraceEvaluation>(position, GetEvaluations, GetEvaluationLevel)?.Hit;

    private TraceMarkIndex<TraceEvaluation>? GetEvaluations(TraceOperatorRow row, TraceOperatorLifetime lifetime)
        => ShowEvaluations ? Evaluations?.GetEvaluations(row.Operator, lifetime.Thread) : null;

    private bool HasEvaluationStrip(TraceOperatorRow row, TraceOperatorLifetime lifetime) => GetEvaluations(row, lifetime) is not null;

    private int GetEvaluationLevel(TraceOperatorRow row, TraceOperatorLifetime lifetime)
        => HasReadStrip(row, lifetime) ? ReadLevel + 1 : ReadLevel;

    private void DrawEvaluationStrip(SKCanvas canvas,
                                     TraceOperatorRow row,
                                     TraceOperatorLifetime lifetime,
                                     SKRect bar,
                                     SKColor colour,
                                     double scale)
    {
        if (GetEvaluations(row, lifetime) is not { } evaluations || GetStrip(bar, GetEvaluationLevel(row, lifetime)) is not { } strip)
        {
            return;
        }

        DrawStrip(canvas,
                  bar,
                  strip,
                  TimelineColours.Scale(colour, ReturnStripShade),
                  evaluations,
                  [.. EvaluationStyles.Select(s => (Func<TraceEvaluation, SKColor?>)(e => e.Result == s.Result ? s.Colour : null))],
                  _ => true,
                  scale);
    }

    private void DrawEvaluationHover(SKCanvas canvas, double scale)
        => DrawMarkHover(canvas,
                         Hover.Evaluation,
                         Hover.Evaluation?.Operator,
                         Hover.Evaluation?.Thread ?? 0,
                         GetEvaluations,
                         GetEvaluationLevel,
                         scale);

    private string GetEvaluationToolTip(TraceEvaluation evaluation)
    {
        var lines = new List<string>
        {
            EvaluationsTitle,
            $"Result {GetEvaluationLabel(evaluation.Result)}",
            evaluation.Node.Symbol,
            $"Thread {evaluation.Thread}  ·  Call {evaluation.Call + 1:N0}",
            GetMarkPosition(evaluation.Position)
        };

        return string.Join(Environment.NewLine, lines);
    }

    private string GetEvaluationStripToolTip(TraceOperatorHit hit)
    {
        var evaluations = GetEvaluations(Rows[hit.Row], Rows[hit.Row].Lifetimes[hit.Track])?.All ?? [];

        var lines = new List<string> { EvaluationsTitle };

        foreach (var style in EvaluationStyles.OrderBy(s => s.Result))
        {
            if (evaluations.Count(e => e.Result == style.Result) is var count and > 0)
            {
                lines.Add($"{style.Label} {count:N0}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetEvaluationLabel(TraceEvaluationResult result)
        => EvaluationStyles.FirstOrDefault(s => s.Result == result).Label ?? result.ToString();

    private readonly record struct EvaluationStyle(TraceEvaluationResult Result, SKColor Colour, string Label);
}
