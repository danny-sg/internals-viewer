using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Services.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Query.Trace.OperatorTracks;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryViewModel
{
    public OperatorTracksViewModel OperatorTracks { get; }

    private TraceTickTrackStore TickTrackStore { get; }

    private Dictionary<TraceTickDefinition, TraceTickSeries> RuleSeries { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<string, TickTrackCache> TrackSeries { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddTickTrack(TraceTickDefinition definition) => TickTrackStore.Add(definition);

    public void ReplaceTickTrack(TraceTickDefinition existing, TraceTickDefinition definition)
        => TickTrackStore.Replace(existing, definition);

    public IReadOnlyList<TraceFunctionChoice> GetFunctionChoices()
        => [.. (CallTree?.Nodes() ?? [])
               .Where(n => n.Frame is { Function: not null } && n.Symbol.Length > 0)
               .Select(n => new TraceFunctionChoice(n.Frame!.Module.Name, n.Symbol, n.Frame))
               .DistinctBy(c => (c.Module.ToLowerInvariant(), c.Symbol))
               .OrderBy(c => c.Symbol, StringComparer.OrdinalIgnoreCase)];

    public async Task<ArgumentLayout?> GetLayoutAsync(TraceFunctionChoice function)
        => Arguments.Lookup is { } lookup ? await lookup.GetLayoutAsync(function.Frame) : null;

    public IReadOnlyList<string> GetOperatorChoices()
        => [.. Events.OfType<ExecutionOperatorEvent>().Select(o => o.Label).Distinct()];

    public IReadOnlyList<string> GetTrackNames()
        => [.. TickTrackStore.Definitions.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase)];

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
        ClearTickTrackCache();

        TickTracks = [];
    }

    private void ClearTickTrackCache()
    {
        RuleSeries.Clear();

        TrackSeries.Clear();
    }

    private void RefreshTickTracks()
    {
        if (Trace is not { } trace)
        {
            ResetTickTracks();

            return;
        }

        var definitions = TickTrackStore.Definitions;

        foreach (var stale in RuleSeries.Keys.Where(k => !definitions.Contains(k, ReferenceEqualityComparer.Instance)).ToList())
        {
            RuleSeries.Remove(stale);
        }

        var groups = definitions.GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();

        var names = groups.Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stale in TrackSeries.Keys.Where(k => !names.Contains(k)).ToList())
        {
            TrackSeries.Remove(stale);
        }

        List<TraceTickTrack> tracks = [];

        for (var index = 0; index < groups.Count; index++)
        {
            TraceTickDefinition[] rules = [.. groups[index]];

            if (!TrackSeries.TryGetValue(groups[index].Key, out var cache)
                || !cache.Rules.SequenceEqual(rules, ReferenceEqualityComparer.Instance))
            {
                var series = TraceTickSeries.FromRules(trace, rules, r => GetRuleSeries(trace, r));

                cache = new TickTrackCache(rules, series);

                TrackSeries[groups[index].Key] = cache;
            }

            tracks.Add(TraceTickTracks.Create(trace, rules, cache.Series, index, TickTrackStore.IsAdded));
        }

        TickTracks = tracks;
    }

    private TraceTickSeries GetRuleSeries(TraceResult trace, TraceTickDefinition rule)
    {
        if (!RuleSeries.TryGetValue(rule, out var series))
        {
            series = TraceTickSeries.FromDefinition(trace, rule);

            RuleSeries[rule] = series;
        }

        return series;
    }

    private sealed record TickTrackCache(TraceTickDefinition[] Rules, TraceTickTrackSeries Series);
}
