using System.Collections.ObjectModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using Windows.UI;

namespace InternalsViewer.UI.App.Models.Query.Trace.Steps;

public sealed record TraceCounterSpan() : AccessStep(AccessPhase.Walk), ITraceSpan
{
    public string Label { get; init; } = string.Empty;

    public ObservableCollection<TraceCounter> Items { get; } = [];

    public TraceFill Fill { get; } = new();

    public bool IsComplete { get; set; }

    public TraceCounterSpan Set(string name, object? value, TraceCounterKind kind = TraceCounterKind.Pair,
                                Color? colour = null, string? format = null)
    {
        Counter(name, kind, colour).Update(value, format);

        return this;
    }

    public TraceCounterSpan SetOnce(string name, object? value, TraceCounterKind kind = TraceCounterKind.Pair,
                                    Color? colour = null)
    {
        if (Find(name) is null)
        {
            Set(name, value, kind, colour);
        }

        return this;
    }

    public TraceCounterSpan Add(string name, long amount, TraceCounterKind kind = TraceCounterKind.Pair,
                                Color? colour = null)
    {
        var counter = Counter(name, kind, colour);

        counter.Update(counter.Number + amount, null);

        return this;
    }

    public long Increment(string name, TraceCounterKind kind = TraceCounterKind.Pair, Color? colour = null)
    {
        var counter = Counter(name, kind, colour);

        counter.Update(counter.Number + 1, null);

        return counter.Number;
    }

    public long Number(string name) => Find(name)?.Number ?? 0;

    private TraceCounter? Find(string name)
    {
        foreach (var counter in Items)
        {
            if (counter.Name == name)
            {
                return counter;
            }
        }

        return null;
    }

    private TraceCounter Counter(string name, TraceCounterKind kind, Color? colour)
    {
        if (Find(name) is { } existing)
        {
            return existing;
        }

        var created = new TraceCounter { Name = name, Kind = kind, Colour = colour ?? TraceCounterColours.Neutral };

        Items.Add(created);

        return created;
    }
}
