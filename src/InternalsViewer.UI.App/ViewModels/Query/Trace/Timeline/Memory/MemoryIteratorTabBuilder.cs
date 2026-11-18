using System;
using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.DataPath;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using static InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory.MemoryFieldTableBuilder;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

internal sealed class MemoryIteratorTabBuilder
{
    public const string DataPathHeader = "Data Path";

    private const int MaximumEntries = 64;

    private const string InputsHeader = "Inputs";

    private const string RowBindingsHeader = "Row Bindings";

    private Dictionary<ulong, string>? ValueColumns { get; set; }

    private Dictionary<(int NodeId, ulong Reference), DataPathGraph?> Graphs { get; } = [];

    public void Clear()
    {
        ValueColumns = null;

        Graphs.Clear();
    }

    public List<MemoryObjectTab> CreateTabs(PlayheadMemoryViewModel owner,
                                            IteratorTarget target,
                                            string? className,
                                            LayoutObject root,
                                            MemoryHistoryReader memory)
    {
        var tabs = new List<MemoryObjectTab>();

        if (target.Type == IteratorTargetType.PlanObject)
        {
            if (CreateDataPathTab(owner, target, root.Address) is { } dataPath)
            {
                tabs.Add(dataPath);
            }

            return tabs;
        }

        if (CreateInputsTab(owner, memory, root.Address) is { } inputs)
        {
            tabs.Add(inputs);
        }

        if (target.Type == IteratorTargetType.ProfilingIterator)
        {
            return tabs;
        }

        if (IteratorLayout.IsRowset(memory.Layout, className) && CreateRowBindingsTab(owner, memory, root.Address) is { } bindings)
        {
            tabs.Add(bindings);
        }

        for (var index = 0; index < root.Fields.Count; index++)
        {
            if (memory.Layout.HashIterator.ValueArrayFields.Contains(root.Fields[index])
                && root.Rows[0][index] is { } array
                && CreateValuesTab(owner, memory, root.Fields[index], array) is { } values)
            {
                tabs.Add(values);
            }
        }

        return tabs;
    }

    public void Refresh(MemoryHistoryViewModel history, MemoryHistoryReader memory)
    {
        foreach (var graph in Graphs.Values)
        {
            if (graph is not null)
            {
                DataPathGraphBuilder.Refresh(graph, history, memory);
            }
        }
    }

    private static MemoryObjectTab? CreateInputsTab(PlayheadMemoryViewModel owner, MemoryHistoryReader memory, ulong address)
    {
        var targets = owner.History.IteratorTargets;

        if (IteratorLayout.GetInputs(memory, address, targets) is not { Count: > 0 } inputs)
        {
            return null;
        }

        var columns = new List<ResultColumn>
        {
            new(0, "Input", typeof(string), false) { Width = IndexWidth, Alignment = ResultAlignment.Right },
            new(1, "Iterator", typeof(string), false) { Width = FieldWidth },
            new(2, "Address", typeof(string), true) { Width = ValueWidth }
        };

        var rows = inputs.Select((a, i) => new ResultRow<long>([$"{i}",
                                                                $"{targets[a].TypeName} › {targets[a].Label}",
                                                                owner.GetPointerCell(a)])
        {
            Id = i
        });

        return CreateTab(InputsHeader, new QueryResultSet { Columns = columns, Rows = [.. rows] }, string.Join(",", inputs));
    }

    private static MemoryObjectTab? CreateRowBindingsTab(PlayheadMemoryViewModel owner, MemoryHistoryReader memory, ulong address)
    {
        if (AccessorBindingLayout.ReadScan(memory, address) is not { Bindings: not 0 } scan
            || AccessorBindingLayout.ReadHeader(memory, scan.Bindings) is not { } header
            || header.ColumnCount == 0)
        {
            return null;
        }

        var bindings = LayoutObjectReader.ReadArray(memory,
                                                    RowBindingsHeader,
                                                    AccessorBindingLayout.ColumnsClassName,
                                                    header.Columns,
                                                    header.ColumnCount);

        var table = CreateRowTable(memory.Layout, (_, p) => owner.GetPointerCell(p), bindings, CreateColours(bindings.Fields.Count));

        return CreateTab(RowBindingsHeader, table, string.Join(";", bindings.Rows.Select(r => string.Join(",", r))));
    }

    private static Dictionary<ulong, string> GetValueColumns(MemoryHistoryViewModel history)
    {
        var columns = new Dictionary<ulong, string>();

        foreach (var (address, target) in history.IteratorTargets)
        {
            if (target is not { Type: IteratorTargetType.Iterator, Operator.PlanNodeIdentifier: { } identifier })
            {
                continue;
            }

            var outputs = history.GetOutputColumns(identifier.NodeId);

            var names = history.GetOutputColumnNames(address, outputs.Count);

            for (var index = 0; index < outputs.Count; index++)
            {
                foreach (var link in history.GetOutputPath(identifier.NodeId, outputs[index]).Links)
                {
                    if (link is { Depth: 0, Type: PlanOutputLinkType.Value or PlanOutputLinkType.BufferEntry })
                    {
                        columns.TryAdd(link.Address, $"{names[index]} · {target.Label}");
                    }
                }
            }
        }

        return columns;
    }

    private static string GetValueColumn(MemoryHistoryReader memory, Dictionary<ulong, string> columns, ulong value)
    {
        if (columns.TryGetValue(value, out var column))
        {
            return column;
        }

        return memory.TryRead(value + (ulong)memory.Layout.CachedSwitch.ValueField.Offset, out ulong entry)
               && columns.TryGetValue(entry, out var entryColumn)
            ? entryColumn
            : string.Empty;
    }

    private static void Open(PlayheadMemoryViewModel owner, ulong address)
    {
        if (address != 0 && owner.History.GetBlock(address, owner.Position) >= 0)
        {
            owner.RequestMemory(address);
        }
    }

    private MemoryObjectTab? CreateValuesTab(PlayheadMemoryViewModel owner, MemoryHistoryReader memory, MemoryField field, ulong array)
    {
        var store = owner.History.Store;

        var block = array == 0 ? -1 : store.GetBlock(array, owner.Position);

        if (block < 0)
        {
            return null;
        }

        var end = store.Blocks[block].Address + (ulong)store.Blocks[block].Size;

        var count = (int)Math.Min((end - array) / sizeof(ulong), MaximumEntries);

        var entries = Enumerable.Range(0, count)
                                .Select(i => memory.TryRead(array + (ulong)(i * sizeof(ulong)), out ulong value) ? value : 0)
                                .ToList();

        var valueColumns = ValueColumns ??= GetValueColumns(owner.History);

        var columns = new List<ResultColumn>
        {
            new(0, "Entry", typeof(string), false) { Width = IndexWidth, Alignment = ResultAlignment.Right },
            new(1, "Value", typeof(string), false) { Width = ValueWidth },
            new(2, "Column", typeof(string), true) { Width = FieldWidth }
        };

        var rows = entries.Select((e, i) => new ResultRow<long>([$"{i}",
                                                                 owner.GetPointerCell(e),
                                                                 GetValueColumn(memory, valueColumns, e)])
        {
            Id = i
        });

        return CreateTab(field.Name, new QueryResultSet { Columns = columns, Rows = [.. rows] }, string.Join(",", entries));
    }

    private MemoryObjectTab? CreateDataPathTab(PlayheadMemoryViewModel owner, IteratorTarget target, ulong address)
    {
        if (target.Operator?.PlanNodeIdentifier is not { } identifier)
        {
            return null;
        }

        var history = owner.History;

        var outputs = history.GetOutputColumns(identifier.NodeId);

        var names = history.GetOutputColumnNames(address, outputs.Count);

        var tabs = new List<MemoryObjectTab>();

        DataPathMap? map = null;

        for (var index = 0; index < outputs.Count; index++)
        {
            var key = (identifier.NodeId, outputs[index].Reference);

            if (!Graphs.TryGetValue(key, out var graph))
            {
                map ??= history.GetDataPathMap();

                Graphs[key] = graph = map.GetOutput(identifier.NodeId, index) is { } field
                    ? DataPathGraphBuilder.Build(history, map, [field], a => Open(owner, a))
                    : null;
            }

            if (graph is not null)
            {
                tabs.Add(new MemoryObjectTab(names[index], [], graph: graph));
            }
        }

        return tabs.Count > 0 ? new MemoryObjectTab(DataPathHeader, tabs) : null;
    }
}
