using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.UI.App.Controls.Trace;
using InternalsViewer.UI.App.Models.Query;
using InternalsViewer.UI.App.Services.Query;
using InternalsViewer.UI.App.ViewModels.Query.OperatorTracks;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryViewModel
{
    public OperatorTracksViewModel OperatorTracks { get; }

    private TraceTickTrackStore TickTrackStore { get; }

    private Dictionary<TraceTickDefinition, TraceTickTrack> CustomTickTracks { get; } = new(ReferenceEqualityComparer.Instance);

    public void AddTickTrack(TraceTickDefinition definition) => TickTrackStore.Add(definition);

    public void ReplaceTickTrack(TraceTickDefinition existing, TraceTickDefinition definition)
        => TickTrackStore.Replace(existing, definition);

    public void ClearTickTracks() => TickTrackStore.Clear();

    public IReadOnlyList<TraceFunctionChoice> GetFunctionChoices()
        => [.. (CallStack?.Nodes() ?? [])
               .Where(n => n.Frame is { Resolved: not null } && n.Symbol.Length > 0)
               .Select(n => new TraceFunctionChoice(n.Frame!.Module, n.Symbol, n.Frame))
               .DistinctBy(c => (c.Module.ToLowerInvariant(), c.Symbol))
               .OrderBy(c => c.Symbol, System.StringComparer.OrdinalIgnoreCase)];

    public async Task<ArgumentLayout?> GetLayoutAsync(TraceFunctionChoice function)
        => Arguments.Lookup is { } lookup ? await lookup.GetLayoutAsync(function.Frame) : null;

    public IReadOnlyList<string> GetOperatorChoices()
        => [.. Events.OfType<ExecutionOperatorEvent>().Select(TraceCallCondition.GetOperatorLabel).Distinct()];

    public int? GetTickCount(TraceTickDefinition definition)
        => CustomTickTracks.TryGetValue(definition, out var track) ? track.Series.Count : null;

    [RelayCommand]
    private void ShowOperatorTracks() => Layout.ShowOperatorTracks();

    private void OnTickTrackStoreChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TraceTickTrackStore.Definitions))
        {
            RefreshTickTracks();
        }
    }

    private void ResetTickTracks()
    {
        CustomTickTracks.Clear();

        TickTracks = [];

        OperatorTracks.Refresh();
    }

    private void RefreshTickTracks()
    {
        if (Trace is not { } trace)
        {
            ResetTickTracks();

            return;
        }

        var definitions = TickTrackStore.Definitions;

        foreach (var stale in CustomTickTracks.Keys.Where(k => !definitions.Contains(k, ReferenceEqualityComparer.Instance)).ToList())
        {
            CustomTickTracks.Remove(stale);
        }

        List<TraceTickTrack> custom = [];

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];

            if (!CustomTickTracks.TryGetValue(definition, out var track))
            {
                track = TraceTickTracks.Custom(definition, TraceTickSeries.FromDefinition(trace.Timeline, trace.CallStore, definition));

                CustomTickTracks[definition] = track;
            }

            custom.Add(track with { Ordinal = TraceTickTracks.CustomOrdinal + index });
        }

        TickTracks = [TraceTickTracks.GetRows(trace.RowReturnMarks),
                      TraceTickTracks.Reads(trace.Reads, trace.CallStore),
                      TraceTickTracks.Evaluations(trace.Evaluations),
                      .. custom];

        OperatorTracks.Refresh();
    }
}
