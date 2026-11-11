using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Summary;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Summary;

public sealed class TraceSummaryOperatorsViewModel : TraceSummaryPaneViewModel
{
    private const string OperatorsTitle = "Operators";

    private const string NotAvailable = "No Operators";

    private const string OutputColumnsName = "Output Columns";

    private const string CompiledPlanObjectName = "Compiled Plan Object";

    private const string OperatorKeyPrefix = "operator/";

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
        var store = history.Store;

        var operators = history.IteratorTargets
                               .Where(t => t.Value.Operator?.PlanNodeIdentifier is not null)
                               .GroupBy(t => t.Value.Operator!.PlanNodeIdentifier!.NodeId)
                               .ToDictionary(g => g.Key, g => (Event: g.First().Value.Operator!, Targets: g.ToList()));

        var live = new List<LiveValue>();

        TraceSummaryNode CreateOperator(int nodeId)
        {
            var (operatorEvent, targets) = operators[nodeId];

            var key = OperatorKeyPrefix + nodeId.ToString(CultureInfo.InvariantCulture);

            var iterator = targets.Find(t => t.Value.Type == IteratorTargetType.Iterator) is { Value: not null } own
                ? own.Key
                : targets[0].Key;

            var block = history.GetIteratorBlock(iterator);

            var position = block >= 0 ? store.Blocks[block].FromPosition : TracePosition.Unknown;

            var outputs = history.GetOutputColumns(nodeId) is { Count: > 0 } columns
                ? CreateOutputColumns(history, nodeId, columns, iterator, position, key, live)
                : null;

            var others = targets.Where(t => t.Key != iterator)
                                .OrderBy(t => t.Value.Type == IteratorTargetType.PlanObject ? 0 : 1)
                                .ToList();

            var hasPlanObject = others.Exists(t => t.Value.Type == IteratorTargetType.PlanObject);

            List<TraceSummaryNode> children = [.. others.Select(t => CreateTarget(history, t.Key, t.Value, position, key, outputs))];

            if (outputs is not null && !hasPlanObject)
            {
                children.Add(outputs);
            }

            children.AddRange(operators.Where(o => o.Value.Event.ParentNodeId == nodeId)
                                       .OrderBy(o => o.Key)
                                       .Select(o => CreateOperator(o.Key)));

            return new TraceSummaryNode(operatorEvent.Label, GetBlockText(history, block, iterator), key)
            {
                Block = block,
                Address = iterator,
                Children = children,
                HasChildren = children.Count > 0
            };
        }

        var roots = operators.Where(o => o.Value.Event.ParentNodeId is not { } parent || !operators.ContainsKey(parent))
                             .OrderBy(o => o.Key)
                             .Select(o => CreateOperator(o.Key))
                             .ToList();

        return new LoadedOperators(roots, live);
    }

    private static TraceSummaryNode CreateTarget(MemoryHistoryViewModel history,
                                                 ulong address,
                                                 IteratorTarget target,
                                                 TracePosition position,
                                                 string key,
                                                 TraceSummaryNode? outputs)
    {
        var block = history.GetIteratorBlock(address) is >= 0 and var iteratorBlock
            ? iteratorBlock
            : history.Store.GetBlock(address, position);

        var isPlanObject = target.Type == IteratorTargetType.PlanObject;

        List<TraceSummaryNode> children = isPlanObject && outputs is not null ? [outputs] : [];

        return new TraceSummaryNode(isPlanObject ? CompiledPlanObjectName : target.TypeName,
                                    GetBlockText(history, block, address),
                                    $"{key}/{target.Type}/{address:X}")
        {
            Block = block,
            Address = address,
            Position = isPlanObject ? position : null,
            Children = children,
            HasChildren = children.Count > 0
        };
    }

    private static TraceSummaryNode CreateOutputColumns(MemoryHistoryViewModel history,
                                                        int nodeId,
                                                        IReadOnlyList<PlanOutputColumn> outputs,
                                                        ulong iterator,
                                                        TracePosition position,
                                                        string key,
                                                        List<LiveValue> live)
    {
        var names = history.GetOutputColumnNames(iterator, outputs.Count);

        var columns = new List<TraceSummaryNode>(outputs.Count);

        for (var index = 0; index < outputs.Count; index++)
        {
            var column = outputs[index];

            var name = names[index];

            var columnKey = $"{key}/output/{index}";

            var links = history.GetOutputLinks(nodeId, column);

            List<TraceSummaryNode> children =
            [
                .. links.Where(IsValueLink).Select((l, i) => CreateOutputLink(history, l, $"{columnKey}/{i}", live))
            ];

            var node = new TraceSummaryNode($"{name} · {ColumnBindingText.GetType(column.Type)}",
                                            GetObjectText(history, column.Reference, position),
                                            columnKey)
            {
                Block = history.Store.GetBlock(column.Reference, position),
                Address = column.Reference,
                Position = position,
                Children = children,
                HasChildren = children.Count > 0
            };

            if (links.Count > 0)
            {
                live.Add(new LiveValue(node,
                                       m => PlanOutputValueReader.Read(m, column, links) ?? string.Empty,
                                       m => history.GetOutputRecord(m, links)));
            }

            columns.Add(node);
        }

        return new TraceSummaryNode(OutputColumnsName, GetCountText(outputs.Count, "Column"), $"{key}/outputs")
        {
            Children = columns,
            HasChildren = columns.Count > 0
        };
    }

    private static bool IsValueLink(PlanOutputLink link)
        => link is
        {
            Depth: 0,
            Type: PlanOutputLinkType.Value
                  or PlanOutputLinkType.Reader
                  or PlanOutputLinkType.RowSlot
                  or PlanOutputLinkType.BufferEntry
                  or PlanOutputLinkType.Record
                  or PlanOutputLinkType.Input
        };

    private static TraceSummaryNode CreateOutputLink(MemoryHistoryViewModel history,
                                                     PlanOutputLink link,
                                                     string key,
                                                     List<LiveValue> live)
    {
        var name = link.Type == PlanOutputLinkType.RowSlot
            ? $"{PlanOutputLinkText.GetName(link.Type)} {link.Slot}"
            : PlanOutputLinkText.GetName(link.Type);

        var detail = link.Type == PlanOutputLinkType.Record
            ? GetFieldText(history, link.Address, link.Position)
            : GetObjectText(history, link.Address, link.Position);

        if (link is { Type: PlanOutputLinkType.Reader, Offset: { } readerOffset })
        {
            detail = $"{detail} · Slot {link.Slot} · {PlanOutputLinkText.GetRowText(readerOffset)}";
        }
        else if (link.Type == PlanOutputLinkType.Reader)
        {
            detail = $"{detail} · Slot {link.Slot}";
        }
        else if (link is { Type: PlanOutputLinkType.BufferEntry or PlanOutputLinkType.Record, Offset: { } offset })
        {
            detail = $"{detail} · {PlanOutputLinkText.GetRowText(offset)}";
        }

        var node = new TraceSummaryNode(name, detail, key)
        {
            Block = history.Store.GetBlock(link.Address, link.Position),
            Address = link.Address,
            Position = link.Position
        };

        if (link.Type is PlanOutputLinkType.RowSlot or PlanOutputLinkType.Record)
        {
            live.Add(new LiveValue(node, m => GetReferenceText(history, m, link.Address), _ => null));
        }

        return node;
    }

    private static string GetObjectText(MemoryHistoryViewModel history, ulong address, TracePosition position)
        => GetBlockText(history, history.Store.GetBlock(address, position), address);

    private static string GetBlockText(MemoryHistoryViewModel history, int block, ulong address)
        => block >= 0 && history.Store.Blocks[block].Address == address && history.GetTypeName(block) is { Length: > 0 } type
            ? $"{type} · {GetAddressText(address)}"
            : GetAddressText(address);

    private static string GetReferenceText(MemoryHistoryViewModel history, MemoryHistoryReader memory, ulong address)
    {
        if (!memory.TryRead(address, out ulong reference) || reference == 0)
        {
            return string.Empty;
        }

        if (history.Store.GetBlock(reference, memory.Position) < 0 && history.GetBufferPage(reference, memory.Position) is { } page)
        {
            var label = history.GetBufferPageLabel(page);

            return history.GetBufferSlot(page) is { } slot ? $"{label} · Slot {slot}" : label;
        }

        return GetFieldText(history, reference, memory.Position);
    }

    private static string GetFieldText(MemoryHistoryViewModel history, ulong address, TracePosition position)
    {
        var block = history.Store.GetBlock(address, position);

        return block >= 0 && history.GetTypeName(block) is { Length: > 0 } type
            ? $"{type} +0x{address - history.Store.Blocks[block].Address:X} · {GetAddressText(address)}"
            : GetAddressText(address);
    }

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
