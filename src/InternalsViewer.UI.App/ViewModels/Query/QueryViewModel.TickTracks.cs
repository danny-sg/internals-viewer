using System;
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
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Tracks;
using InternalsViewer.Query.Plans;
using InternalsViewer.Query.Plans.Enums;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.Query.Plans.Operators;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Services.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Query.Trace.OperatorTracks;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryViewModel
{
    public OperatorTracksViewModel OperatorTracks { get; }

    private TraceTickTrackStore TickTrackStore { get; }

    private Dictionary<TraceTickDefinition, TraceTickSeries> RuleSeries { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<TraceTickDefinition, TraceWatch> RuleWatches { get; } = new(ReferenceEqualityComparer.Instance);

    private Dictionary<string, TickTrackCache> TrackSeries { get; } = new(StringComparer.OrdinalIgnoreCase);

    private ILookup<(PlanNode Puller, ExpressionPullSite Site), ExpressionPull> ExpressionPulls
        => CreatedExpressionPulls ??= ExecutionPlans.SelectMany(ExpressionPullResolver.Resolve).ToLookup(p => (p.Puller, p.Site));

    private ILookup<(PlanNode Puller, ExpressionPullSite Site), ExpressionPull>? CreatedExpressionPulls { get; set; }

    private Dictionary<PlanNode, Dictionary<string, PredicateText>> PlanValues { get; } = [];

    private Dictionary<PlanNode, List<(string Column, SqlDbType? Type)>> SeekKeyColumns { get; } = [];

    public void AddTickTrack(TraceTickDefinition definition) => TickTrackStore.Add(definition);

    public void ReplaceTickTrack(TraceTickDefinition existing, TraceTickDefinition definition)
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
        CreatedExpressionPulls = null;

        PlanValues.Clear();

        SeekKeyColumns.Clear();

        RuleSeries.Clear();

        RuleWatches.Clear();

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

        foreach (var stale in RuleWatches.Keys.Where(k => !definitions.Contains(k, ReferenceEqualityComparer.Instance)).ToList())
        {
            RuleWatches.Remove(stale);
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

            tracks.Add(TraceTickTracks.Create(trace,
                                              rules,
                                              cache.Series,
                                              index,
                                              TickTrackStore.IsAdded,
                                              GetParentOperatorName,
                                              GetStepPredicate,
                                              r => GetRuleWatch(trace, r)));
        }

        TickTracks = tracks;
    }

    private TraceTickSeries GetRuleSeries(TraceResult trace, TraceTickDefinition rule)
    {
        if (rule.Source == TraceTickSourceType.Watch)
        {
            return GetRuleWatch(trace, rule).Series;
        }

        if (!RuleSeries.TryGetValue(rule, out var series))
        {
            var statement = GetStatementOperator();

            series = rule.Source switch
            {
                TraceTickSourceType.Evaluate
                    => TraceTickSeries.FromAssignedCalls(trace,
                                                         rule,
                                                         statement,
                                                         p => GetPulledComputeScalars(p, (ExpressionPullSite)rule.Value)),
                TraceTickSourceType.Statement
                    => TraceTickSeries.FromAssignedCalls(trace, rule, statement, _ => statement is null ? [] : [statement]),
                _ => TraceTickSeries.FromDefinition(trace, rule)
            };

            RuleSeries[rule] = series;
        }

        return series;
    }

    private ExecutionOperatorEvent? GetStatementOperator()
        => Events.OfType<ExecutionOperatorEvent>().FirstOrDefault(o => GetPlanNode(o) is { IsStatement: true });

    private List<ExecutionOperatorEvent> GetPulledComputeScalars(ExecutionOperatorEvent puller, ExpressionPullSite site)
    {
        if (GetPlanNode(puller) is not { } node)
        {
            return [];
        }

        var operators = Events.OfType<ExecutionOperatorEvent>().ToList();

        return [.. ExpressionPulls[(node, site)].Select(p => operators.Find(o => ReferenceEquals(GetPlanNode(o), p.ComputeScalar)))
                                                .OfType<ExecutionOperatorEvent>()];
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
        if (name.Equals(TraceTickDefinition.ExpressionPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            return GetEvaluatedValues(tick).GetText(GetPlanExpressions(tick.Operator));
        }

        if (!name.StartsWith(TraceTickDefinition.ExecutionPlanPrefix, StringComparison.OrdinalIgnoreCase)
            || GetPlanNode(tick.Operator) is not { } node)
        {
            return null;
        }

        if (!PlanValues.TryGetValue(node, out var values))
        {
            values = PlanNodePropertyBuilder.GetValues(node, GetPlanExpressions(tick.Operator));

            PlanValues[node] = values;
        }

        return name.Equals(TraceTickDefinition.CurrentRangePlaceholder, StringComparison.OrdinalIgnoreCase)
            ? GetCurrentRange(tick, node, values)
            : values.GetValueOrDefault(name[TraceTickDefinition.ExecutionPlanPrefix.Length..]);
    }

    private PredicateText? GetCurrentRange(TraceTick tick, PlanNode node, Dictionary<string, PredicateText> values)
        => Trace is not { } trace
            ? PlanNodePropertyBuilder.GetSeekRange(values, 0)
            : GetSeekKeyText(trace, tick, node) ?? PlanNodePropertyBuilder.GetSeekRange(values, trace.GetSeekRange(tick));

    private PredicateText? GetSeekKeyText(TraceResult trace, TraceTick tick, PlanNode node)
    {
        return node.PredicateInfo is { IsCorrelatedEquality: true } info
            ? TraceTickTracks.GetSeekKeyText(GetSeekKeyColumns(node, info), trace.GetSeekKeys(tick))
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

    private TraceWatch GetRuleWatch(TraceResult trace, TraceTickDefinition rule)
    {
        if (!RuleWatches.TryGetValue(rule, out var watch))
        {
            watch = TraceWatch.Build(trace, rule);

            RuleWatches[rule] = watch;
        }

        return watch;
    }

    private sealed record TickTrackCache(TraceTickDefinition[] Rules, TraceTickTrackSeries Series);
}
