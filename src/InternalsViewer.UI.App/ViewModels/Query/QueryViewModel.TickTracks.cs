using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Joins;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Services.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Query.Trace.OperatorTracks;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryViewModel
{
    public OperatorTracksViewModel OperatorTracks { get; }

    private TraceTickTrackStore TickTrackStore { get; }

    private Dictionary<string, TickTrackCache> TrackSeries { get; } = new(StringComparer.OrdinalIgnoreCase);

    private ILookup<(PlanNode Puller, ExpressionPullSite Site), ExpressionPull> ExpressionPulls
        => CreatedExpressionPulls ??= ExecutionPlans.SelectMany(ExpressionPullResolver.Resolve).ToLookup(p => (p.Puller, p.Site));

    private ILookup<(PlanNode Puller, ExpressionPullSite Site), ExpressionPull>? CreatedExpressionPulls { get; set; }

    private ConcurrentDictionary<PlanNode, Dictionary<string, PredicateText>> PlanValues { get; } = [];

    private ConcurrentDictionary<PlanNode, List<(string Column, SqlDbType? Type)>> SeekKeyColumns { get; } = [];

    private int TickTrackVersion { get; set; }

    public void AddTickTrack(TraceTrackRule definition) => TickTrackStore.Add(definition);

    public void ReplaceTickTrack(TraceTrackRule existing, TraceTrackRule definition)
        => TickTrackStore.Replace(existing, definition);

    public IReadOnlyList<TraceFunctionChoice> GetFunctionChoices()
        => [.. (CallTree?.GetNodes() ?? [])
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

    public IReadOnlyList<string> GetIteratorClasses()
        => Trace is not { } trace
            ? []
            : [.. trace.Lifetimes
                       .SelectMany(l => l.Instances.Select(i => TraceWatch.GetClassName(trace, l, i)))
                       .OfType<string>()
                       .Distinct(StringComparer.Ordinal)
                       .Order(StringComparer.OrdinalIgnoreCase)];

    private static PlanNode? FindParent(IEnumerable<PlanNode> nodes, PlanNode node)
    {
        foreach (var candidate in nodes)
        {
            if (candidate.Children.Any(c => ReferenceEquals(c, node)))
            {
                return candidate;
            }

            if (FindParent(candidate.Children, node) is { } parent)
            {
                return parent;
            }
        }

        return null;
    }

    [RelayCommand]
    private void ShowOperatorTracks() => Layout.ShowOperatorTracks();

    private string GetParentOperatorName(TraceTick tick)
    {
        if (tick.Caller is { } caller)
        {
            return caller.OperatorDescription;
        }

        return GetPlanNode(tick.Operator) is { } node && GetPlanParent(node) is { } parent
            ? parent.PhysicalOperator
            : string.Empty;
    }

#pragma warning disable VSTHRD100
    private async void OnTickTrackStoreChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TraceTickTrackStore.Definitions))
        {
            return;
        }

        try
        {
            await RefreshTickTracksAsync();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "The tick tracks could not be built");
        }
    }
#pragma warning restore VSTHRD100

    private void ResetTickTracks()
    {
        TickTrackVersion++;

        ClearTickTrackCache();

        TickTracks = [];
    }

    private void ClearTickTrackCache()
    {
        CreatedExpressionPulls = null;

        PlanValues.Clear();

        SeekKeyColumns.Clear();

        TrackSeries.Clear();
    }

    private async Task RefreshTickTracksAsync()
    {
        var version = ++TickTrackVersion;

        if (Trace is not { } trace)
        {
            ResetTickTracks();

            return;
        }

        List<TraceTrackRule[]> groups = [.. TickTrackStore.Definitions.GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                                                                      .Select(g => g.ToArray())];

        var cached = groups.Select(GetCachedSeries).ToList();

        var statement = GetStatementOperator();

        var getPulled = GetPulledComputeScalarLookup();

        var built = await Task.Run(() => groups.Select((g, i) => cached[i] ?? TraceTrackBuilder.Build(trace, g, statement, getPulled))
                                               .ToList());

        if (version != TickTrackVersion || !ReferenceEquals(trace, Trace))
        {
            return;
        }

        TrackSeries.Clear();

        List<TraceTickTrack> tracks = [];

        for (var index = 0; index < groups.Count; index++)
        {
            var series = built[index];

            TrackSeries[groups[index][0].Name] = new TickTrackCache(groups[index], series);

            tracks.Add(TraceTickTracks.Create(trace,
                                              groups[index],
                                              series,
                                              index,
                                              TickTrackStore.IsAdded,
                                              GetParentOperatorName,
                                              GetStepPredicate,
                                              series.GetWatch));
        }

        TickTracks = tracks;
    }

    private TraceTickTrackSeries? GetCachedSeries(TraceTrackRule[] rules)
        => TrackSeries.TryGetValue(rules[0].Name, out var cache) && cache.Rules.SequenceEqual(rules, ReferenceEqualityComparer.Instance)
            ? cache.Series
            : null;

    private ExecutionOperatorEvent? GetStatementOperator()
        => Events.OfType<ExecutionOperatorEvent>().FirstOrDefault(o => GetPlanNode(o) is { IsStatement: true });

    private Func<ExecutionOperatorEvent, ExpressionPullSite, List<ExecutionOperatorEvent>> GetPulledComputeScalarLookup()
    {
        var operators = Events.OfType<ExecutionOperatorEvent>().ToList();

        var pulls = ExpressionPulls;

        return (puller, site) => GetPlanNode(puller) is { } node
            ? [.. pulls[(node, site)].Select(p => operators.Find(o => ReferenceEquals(GetPlanNode(o), p.ComputeScalar)))
                                     .OfType<ExecutionOperatorEvent>()]
            : [];
    }

    private IReadOnlyList<DefinedValueInfo> GetEvaluatedValues(TraceTick tick)
    {
        if (GetPlanNode(tick.Operator) is not { } computeScalar)
        {
            return [];
        }

        var puller = tick.Caller is { } caller ? GetPlanNode(caller) : null;

        var pull = ExpressionPulls.SelectMany(p => p)
                                  .Where(p => ReferenceEquals(p.ComputeScalar, computeScalar))
                                  .FirstOrDefault(p => ReferenceEquals(p.Puller, puller));

        return pull?.Values ?? computeScalar.DefinedValues;
    }

    private PredicateText? GetStepPredicate(TraceTick tick, string name)
    {
        if (name.Equals(TraceTrackRule.ExpressionPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            return GetEvaluatedValues(tick).GetText(GetPlanExpressions(tick.Operator));
        }

        if (!name.StartsWith(TraceTrackRule.ExecutionPlanPrefix, StringComparison.OrdinalIgnoreCase)
            || GetPlanNode(tick.Operator) is not { } node)
        {
            return null;
        }

        if (!PlanValues.TryGetValue(node, out var values))
        {
            values = PlanNodePropertyBuilder.GetValues(node, GetPlanExpressions(tick.Operator));

            PlanValues[node] = values;
        }

        return name.Equals(TraceTrackRule.CurrentRangePlaceholder, StringComparison.OrdinalIgnoreCase)
            ? GetCurrentRange(tick, node, values)
            : values.GetValueOrDefault(name[TraceTrackRule.ExecutionPlanPrefix.Length..]);
    }

    private PredicateText? GetCurrentRange(TraceTick tick, PlanNode node, Dictionary<string, PredicateText> values)
        => Trace is not { } trace
            ? PlanNodePropertyBuilder.GetSeekRange(values, 0)
            : GetRowIdText(trace, tick, node)
              ?? GetSeekKeyText(trace, tick, node)
              ?? PlanNodePropertyBuilder.GetSeekRange(values, trace.SourceTracks.GetSeekRange(tick));

    private static PredicateText? GetRowIdText(TraceResult trace, TraceTick tick, PlanNode node)
        => CorrelatedJoinResolver.IsRidLookup(node) && TraceTickTracks.GetRowId(trace, tick) is { } rowId
            ? TraceTickTracks.GetRowIdText(rowId)
            : null;

    private PredicateText? GetSeekKeyText(TraceResult trace, TraceTick tick, PlanNode node)
    {
        return node.PredicateInfo is { IsCorrelatedEquality: true } info
            ? TraceTickTracks.GetSeekKeyText(GetSeekKeyColumns(node, info), trace.SourceTracks.GetSeekKeys(tick))
            : null;
    }

    private List<(string Column, SqlDbType? Type)> GetSeekKeyColumns(PlanNode node, PredicateInfo info)
    {
        if (!SeekKeyColumns.TryGetValue(node, out var columns))
        {
            columns = [.. info.CorrelatedSeekColumns.Select(c => (c.Column, GetColumnType(node, c.Column)))];

            SeekKeyColumns[node] = columns;
        }

        return columns;
    }

    private SqlDbType? GetColumnType(PlanNode node, string name)
    {
        if (FindAllocationUnit(node) is not { } unit)
        {
            return null;
        }

        var structure = IndexStructureProvider.GetIndexStructure(Database, unit.AllocationUnitId);

        return structure.Columns.Concat(structure.TableStructure?.Columns ?? [])
                                .FirstOrDefault(c => string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                                ?.DataType;
    }

    private ExpressionCatalog? GetPlanExpressions(ExecutionOperatorEvent operatorEvent)
    {
        var plan = ExecutionPlans.FirstOrDefault(p => p.PlanHandleId == operatorEvent.PlanNodeIdentifier?.PlanHandleId);

        return (plan ?? ExecutionPlans.FirstOrDefault())?.Expressions;
    }

    private sealed record TickTrackCache(TraceTrackRule[] Rules, TraceTickTrackSeries Series);
}
