using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Models.Query.Trace.Summary;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;

public sealed class TraceSummaryOperatorsViewModel : TraceSummaryPaneViewModel
{
    private const string OperatorsTitle = "Operators";

    private const string NotAvailable = "No Operators";

    private const string OutputColumnsName = "Output Columns";

    private const string CompiledPlanObjectName = "Compiled Plan Object";

    private const string OperatorKeyPrefix = "operator/";

    private const string StatementKey = "statement";

    private static readonly TimeSpan ValueInterval = TimeSpan.FromMilliseconds(50);

    public TraceSummaryOperatorsViewModel(MemoryHistoryViewModel history, Func<ITraceNavigator> getNavigator)
        : base(OperatorsTitle, history, getNavigator)
    {
        Loader = new PlayheadLoader<LoadedOperators?>(TimeSpan.Zero, Apply, b => IsLoading = b);

        ValueLoader = new PlayheadLoader<LiveValues>(ValueInterval, ApplyValues);
    }

    private PlayheadLoader<LoadedOperators?> Loader { get; }

    private PlayheadLoader<LiveValues> ValueLoader { get; }

    private List<LiveValue> Live { get; set; } = [];

    private TracePosition? Position { get; set; }

    private int Version { get; set; }

    public override void SetPlayhead(TracePosition position)
    {
        Position = position;

        RequestValues();
    }

    protected override void Build()
    {
        if (!History.IsAvailable)
        {
            EmptyText = NotAvailable;

            return;
        }

        EmptyText = string.Empty;

        var history = History;

        Loader.Request(() => Read(history));
    }

    protected override void Clear()
    {
        Loader.Cancel();

        ValueLoader.Cancel();

        Live = [];
    }

    private static LoadedOperators Read(MemoryHistoryViewModel history)
    {
        var operators = history.IteratorTargets
                               .Where(t => t.Value.Operator?.PlanNodeIdentifier is not null)
                               .GroupBy(t => t.Value.Operator!.PlanNodeIdentifier!.NodeId)
                               .ToDictionary(g => g.Key, g => (Event: g.First().Value.Operator!, Targets: g.ToList()));

        var live = new List<LiveValue>();

        TraceSummaryNode? CreateColumns(int nodeId, string key)
        {
            var targets = operators[nodeId].Targets;

            var planObject = targets.Find(t => t.Value.Type == IteratorTargetType.PlanObject).Key;

            return history.GetOutputColumns(nodeId) is { Count: > 0 } columns
                ? CreateOutputColumns(history, nodeId, columns, targets[0].Key, planObject, GetPosition(history, targets), key, live)
                : null;
        }

        TraceSummaryNode CreateOperator(int nodeId)
        {
            var (operatorEvent, targets) = operators[nodeId];

            var key = OperatorKeyPrefix + nodeId.ToString(CultureInfo.InvariantCulture);

            var iterator = targets.Find(t => t.Value.Type == IteratorTargetType.Iterator);

            var planObject = targets.Find(t => t.Value.Type == IteratorTargetType.PlanObject);

            var position = GetPosition(history, targets);

            var children = new List<TraceSummaryNode>();

            if (planObject.Value is not null)
            {
                children.Add(CreateTarget(history, planObject.Key, CompiledPlanObjectName, position, key, position));
            }

            if (iterator.Value is not null)
            {
                children.Add(CreateTarget(history, iterator.Key, iterator.Value.TypeName, position, key, null));
            }

            if (CreateColumns(nodeId, key) is { } outputs)
            {
                children.Add(outputs);
            }

            children.AddRange(operators.Where(o => o.Value.Event.ParentNodeId == nodeId)
                                       .OrderBy(o => o.Key)
                                       .Select(o => CreateOperator(o.Key)));

            return new TraceSummaryNode(operatorEvent.Label, string.Empty, key)
            {
                Children = children,
                HasChildren = children.Count > 0
            };
        }

        var tops = operators.Where(o => o.Value.Event.ParentNodeId is not { } parent || !operators.ContainsKey(parent))
                            .OrderBy(o => o.Key)
                            .ToList();

        List<TraceSummaryNode> roots = [.. tops.Select(o => CreateOperator(o.Key))];

        if (tops.Count > 0
            && tops[0].Value.Event.PlanNodeIdentifier is { } identifier
            && history.GetStatementName(identifier) is { } statement)
        {
            List<TraceSummaryNode> children = tops.Count == 1 && CreateColumns(tops[0].Key, StatementKey) is { } outputs
                ? [outputs, .. roots]
                : roots;

            roots = [new TraceSummaryNode(statement, string.Empty, StatementKey) { Children = children, HasChildren = true }];
        }

        return new LoadedOperators(roots, live);
    }

    private static TracePosition GetPosition(MemoryHistoryViewModel history, List<KeyValuePair<ulong, IteratorTarget>> targets)
    {
        var iterator = targets.Find(t => t.Value.Type == IteratorTargetType.Iterator);

        var block = history.GetIteratorBlock(iterator.Value is null ? targets[0].Key : iterator.Key);

        return block >= 0 ? history.Store.Blocks[block].FromPosition : TracePosition.Unknown;
    }

    private static int GetTargetBlock(MemoryHistoryViewModel history, ulong address, TracePosition position)
        => history.GetIteratorBlock(address) is >= 0 and var block ? block : history.Store.GetBlock(address, position);

    private static TraceSummaryNode CreateTarget(MemoryHistoryViewModel history,
                                                 ulong address,
                                                 string name,
                                                 TracePosition position,
                                                 string key,
                                                 TracePosition? openAt)
    {
        var block = GetTargetBlock(history, address, position);

        return new TraceSummaryNode(name, GetBlockText(history, block, address), $"{key}/{name}/{address:X}")
        {
            Block = block,
            Address = address,
            Position = openAt
        };
    }

    private static TraceSummaryNode CreateOutputColumns(MemoryHistoryViewModel history,
                                                        int nodeId,
                                                        IReadOnlyList<PlanOutputColumn> outputs,
                                                        ulong target,
                                                        ulong planObject,
                                                        TracePosition position,
                                                        string key,
                                                        List<LiveValue> live)
    {
        var names = history.GetOutputColumnNames(target, outputs.Count);

        var planBlock = planObject == 0 ? -1 : GetTargetBlock(history, planObject, position);

        var columns = new List<TraceSummaryNode>(outputs.Count);

        for (var index = 0; index < outputs.Count; index++)
        {
            var column = outputs[index];

            var node = new TraceSummaryNode(names[index], string.Empty, $"{key}/output/{index}")
            {
                Block = planBlock,
                Address = planObject,
                Position = position,
                ColumnType = column.Type,
                LineageColumn = planBlock >= 0 ? names[index] : null
            };

            if (history.GetOutputPath(nodeId, column).Stages is [var stage, ..])
            {
                live.Add(new LiveValue(node,
                                       m => PlanOutputValueReader.Read(m, stage) ?? string.Empty,
                                       m => history.GetOutputRecord(m, stage)));
            }

            columns.Add(node);
        }

        return new TraceSummaryNode(OutputColumnsName, GetCountText(outputs.Count, "Column"), $"{key}/outputs")
        {
            Children = columns,
            HasChildren = columns.Count > 0
        };
    }

    private static string GetBlockText(MemoryHistoryViewModel history, int block, ulong address)
        => block >= 0 && history.Store.Blocks[block].Address == address && history.GetTypeName(block) is { Length: > 0 } type
            ? $"{type} · {GetAddressText(address)}"
            : GetAddressText(address);

    private static LiveValues ReadValues(MemoryHistoryStore store, List<LiveValue> live, TracePosition position)
    {
        var memory = new MemoryHistoryReader(store, position);

        return new LiveValues(live, [.. live.Select(l => l.Read(memory))], [.. live.Select(l => l.GetRecord(memory))]);
    }

    private void Apply(LoadedOperators? loaded)
    {
        if (loaded is not { Roots.Count: > 0 })
        {
            EmptyText = NotAvailable;

            return;
        }

        Live = loaded.Live;

        Roots = loaded.Roots;

        RequestValues();
    }

    private void RequestValues()
    {
        if (Position is not { } position || Live.Count == 0)
        {
            return;
        }

        var live = Live;

        var store = History.Store;

        ValueLoader.Request(() => ReadValues(store, live, position));
    }

    private void ApplyValues(LiveValues values)
    {
        if (!ReferenceEquals(values.Live, Live))
        {
            return;
        }

        var version = ++Version;

        for (var index = 0; index < values.Live.Count; index++)
        {
            if (values.Records[index] is { } record)
            {
                _ = ApplyRecordAsync(values.Live[index].Node, record, version);
            }
            else
            {
                values.Live[index].Node.Value = values.Values[index];
            }
        }
    }

    private async Task ApplyRecordAsync(TraceSummaryNode node, PlanOutputRecord record, int version)
    {
        var value = await History.ReadRecordAsync(record);

        if (version == Version)
        {
            node.Value = value ?? string.Empty;
        }
    }

    private readonly record struct LiveValue(TraceSummaryNode Node,
                                             Func<MemoryHistoryReader, string> Read,
                                             Func<MemoryHistoryReader, PlanOutputRecord?> GetRecord);

    private sealed record LiveValues(List<LiveValue> Live, string[] Values, PlanOutputRecord?[] Records);

    private sealed record LoadedOperators(List<TraceSummaryNode> Roots, List<LiveValue> Live);
}
