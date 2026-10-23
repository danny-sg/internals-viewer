using System;
using System.Collections.Generic;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.UI.App.Models.Query.Trace.Enums;
using SkiaSharp;

namespace InternalsViewer.UI.App.Models.Query.Trace;

public sealed record TraceTickTrack(string Name, TraceTickLocation Location, int Ordinal, TraceTickSeries Series)
{
    public string MenuText { get; init; } = Name;

    public IReadOnlyList<TraceTickValue> Values { get; init; } = [];

    public Func<TraceTick, SKColor?> GetColour { get; init; } = _ => null;

    public SKColor? Colour { get; init; }

    public Func<ExecutionOperatorEvent, SKColor?>? GetTrackColour { get; init; }

    public SKColor? Background { get; init; }

    public bool HasCallReturnLine { get; init; }

    public bool IsStatementTrack { get; init; }

    public float TickWidth { get; init; } = 1f;

    public float HeightFactor { get; init; } = 1f;

    public float WidthFactor { get; init; } = 1f;

    public Func<ExecutionOperatorEvent, string>? GetTitle { get; init; }

    public Func<TraceTick, IEnumerable<string>>? GetDetails { get; init; }

    public TraceTickDefinition? Definition { get; init; }

    public bool IsCustom => Definition is not null;
}
