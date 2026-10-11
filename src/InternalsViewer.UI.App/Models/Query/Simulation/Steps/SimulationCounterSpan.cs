using System.Collections.ObjectModel;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using Windows.UI;

namespace InternalsViewer.UI.App.Models.Query.Simulation.Steps;

public sealed record SimulationCounterSpan() : AccessStep(AccessPhase.Walk), ISimulationSpan
{
    public string Label { get; init; } = string.Empty;

    public ObservableCollection<SimulationCounter> Items { get; } = [];

    public SimulationFill Fill { get; } = new();

    public bool IsComplete { get; set; }

    public SimulationCounterSpan Set(string name, object? value, SimulationCounterKind kind = SimulationCounterKind.Pair,
                                Color? colour = null, string? format = null)
    {
        Counter(name, kind, colour).Update(value, format);

        return this;
    }

    public SimulationCounterSpan SetOnce(string name, object? value, SimulationCounterKind kind = SimulationCounterKind.Pair,
                                    Color? colour = null)
    {
        if (Find(name) is null)
        {
            Set(name, value, kind, colour);
        }

        return this;
    }

    public SimulationCounterSpan Add(string name, long amount, SimulationCounterKind kind = SimulationCounterKind.Pair,
                                Color? colour = null)
    {
        var counter = Counter(name, kind, colour);

        counter.Update(counter.Number + amount, null);

        return this;
    }

    public long Increment(string name, SimulationCounterKind kind = SimulationCounterKind.Pair, Color? colour = null)
    {
        var counter = Counter(name, kind, colour);

        counter.Update(counter.Number + 1, null);

        return counter.Number;
    }

    public long Number(string name) => Find(name)?.Number ?? 0;

    private SimulationCounter? Find(string name)
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

    private SimulationCounter Counter(string name, SimulationCounterKind kind, Color? colour)
    {
        if (Find(name) is { } existing)
        {
            return existing;
        }

        var created = new SimulationCounter { Name = name, Kind = kind, Colour = colour ?? SimulationCounterColours.Neutral };

        Items.Add(created);

        return created;
    }
}
