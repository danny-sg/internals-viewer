using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Batches;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Lineage;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using SqlTypeInfo = InternalsViewer.Query.FullTrace.Locators.SqlTypeInfo;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public static class LineageGraphBuilder
{
    private const double Margin = 16;

    private const double RowGap = 24;

    private const int PointerSize = sizeof(ulong);

    private const string SlotName = "Slot";

    private static MemoryField VectorField => EngineLayout.BatchAdapter.ColumnMapVectorField;

    public static LineageGraph? Build(MemoryHistoryViewModel history, int nodeId, PlanOutputColumn column, Action<ulong> open)
    {
        var path = history.GetOutputPath(nodeId, column);

        if (!path.Links.Any(l => !l.IsRoute))
        {
            return null;
        }

        var graph = new LineageGraph(nodeId, column, path, open);

        SetStructure(graph, history, path.Links);

        return graph;
    }

    public static void Refresh(LineageGraph graph, MemoryHistoryViewModel history, MemoryHistoryReader memory)
    {
        var links = history.GetOutputLinks(graph.NodeId, graph.Column, memory);

        if (links.Any(l => !l.IsRoute) && GetKey(links) != graph.Key)
        {
            SetStructure(graph, history, links);
        }

        graph.Version++;

        LineageRow? terminal = null;

        foreach (var box in graph.Boxes)
        {
            foreach (var row in box.Rows)
            {
                var address = box.Address + (ulong)row.Offset;

                var pointer = 0UL;

                var hasPointer = row.HasPointer && memory.TryRead(address, out pointer);

                row.Pointer = hasPointer ? pointer : 0;

                row.PointerText = hasPointer ? $"0x{pointer:X16}" : string.Empty;

                row.Value = row.ValueType is { } type ? PlanOutputValueReader.ReadEntry(memory, type, address) ?? row.Detail : row.Detail;

                if (row.IsTerminal)
                {
                    terminal = row;
                }
            }
        }

        if (terminal is not null && PlanOutputValueReader.Read(memory, graph.Column, graph.Path) is { } value)
        {
            terminal.Value = value;
        }
        else if (terminal is not null && history.GetOutputRecord(memory, graph.Path) is { } record)
        {
            _ = ReadRecordAsync(graph, history, terminal, record, graph.Version);
        }

        graph.RaiseChanged();
    }

    private static async Task ReadRecordAsync(LineageGraph graph,
                                              MemoryHistoryViewModel history,
                                              LineageRow row,
                                              PlanOutputRecord record,
                                              int version)
    {
        var value = await history.ReadRecordAsync(record);

        if (value is not null && version == graph.Version)
        {
            row.Value = value;

            graph.RaiseChanged();
        }
    }

    private static void SetStructure(LineageGraph graph, MemoryHistoryViewModel history, IReadOnlyList<PlanOutputLink> links)
    {
        var boxes = new Dictionary<ulong, LineageBox>();

        var order = new List<LineageBox>();

        var terminal = GetTerminal(links);

        var types = new Dictionary<int, SqlTypeInfo?> { [0] = graph.Column.Type };

        var warnings = new List<string>();

        LineageBox GetBox(ulong address, PlanOutputLinkType? type, LineageBox? parent, TracePosition position)
        {
            var start = GetStart(history, address, position);

            if (!boxes.TryGetValue(start, out var box))
            {
                box = new LineageBox(GetTitle(history, start, type, position),
                                     GetSubtitle(history, start, position),
                                     start,
                                     type,
                                     parent);

                boxes[start] = box;

                order.Add(box);
            }

            return box;
        }

        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];

            if (link.Type == PlanOutputLinkType.Input)
            {
                types[link.Depth + 1] = history.GetReferenceType(link.Address);
            }

            var writer = history.GetUnexpectedWriter(link);

            if (writer is not null)
            {
                warnings.Add($"{PlanOutputLinkText.GetName(link.Type)} +0x{link.SourceOffset:X} Written By {writer}");
            }

            if (link.IsRoute)
            {
                continue;
            }

            var source = GetBox(link.Source, null, null, link.Position);

            var offset = (int)(link.Source - source.Address) + link.SourceOffset;

            var isField = link.Address == link.Source + (ulong)link.SourceOffset;

            var isIndex = link.Type == PlanOutputLinkType.RowContext;

            var isEntry = isField && link.Type == PlanOutputLinkType.BufferEntry;

            var isNumber = isField
                           && link.Type is PlanOutputLinkType.Cursor or PlanOutputLinkType.VectorId or PlanOutputLinkType.SelectedRow;

            var row = source.Rows.Find(r => r.Offset == offset);

            if (row is null)
            {
                row = new LineageRow(GetRowName(link, source, offset),
                                     offset,
                                     GetDetail(link),
                                     !isIndex && !isEntry && !isNumber,
                                     isIndex,
                                     index == terminal)
                {
                    ValueType = isEntry ? types.GetValueOrDefault(link.Depth) : null
                };

                source.Rows.Add(row);
            }

            row.UnexpectedWriter ??= writer;

            if (isField || row.Target is not null)
            {
                continue;
            }

            var target = GetBox(link.Address, link.Type, source, link.Position);

            var targetOffset = (int)(link.Address - target.Address);

            row.Target = target;

            row.TargetOffset = targetOffset > 0 || link.Type == PlanOutputLinkType.BufferEntry ? targetOffset : -1;
        }

        var (width, height) = Place(order);

        graph.SetStructure(order, width, height, GetKey(links), warnings);
    }

    private static string GetKey(IReadOnlyList<PlanOutputLink> links)
        => string.Join(";", links.Where(l => !l.IsRoute).Select(l => $"{l.Type}:{l.Address:X}:{l.Source:X}:{l.SourceOffset:X}"));

    private static ulong GetStart(MemoryHistoryViewModel history, ulong address, TracePosition position)
    {
        if (history.IteratorTargets.ContainsKey(address))
        {
            return address;
        }

        var block = history.GetBlock(address, position);

        if (block < 0)
        {
            return address;
        }

        var info = history.Store.Blocks[block];

        return address >= info.Address && address < info.Address + (ulong)info.Size ? info.Address : address;
    }

    private static (double Width, double Height) Place(List<LineageBox> boxes)
    {
        var nextY = new Dictionary<int, double>();

        var width = 0d;

        var height = 0d;

        foreach (var box in boxes)
        {
            box.Rows.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            box.X = Margin + box.Column * (LineageGraph.BoxWidth + LineageGraph.ColumnGap);

            box.Y = Math.Max(nextY.GetValueOrDefault(box.Column, Margin), box.Parent?.Y ?? Margin);

            nextY[box.Column] = box.Y + box.Height + RowGap;

            width = Math.Max(width, box.X + LineageGraph.BoxWidth + Margin);

            height = Math.Max(height, box.Y + box.Height + Margin);
        }

        return (width, height);
    }

    private static int GetTerminal(IReadOnlyList<PlanOutputLink> links)
    {
        for (var index = links.Count - 1; index >= 0; index--)
        {
            if (links[index] is
                {
                    IsRoute: false,
                    Depth: 0,
                    Type: PlanOutputLinkType.BufferEntry or PlanOutputLinkType.RowSlot or PlanOutputLinkType.Record
                })
            {
                return index;
            }
        }

        return -1;
    }

    private static string GetTitle(MemoryHistoryViewModel history, ulong address, PlanOutputLinkType? type, TracePosition position)
    {
        if (history.IteratorTargets.TryGetValue(address, out var target))
        {
            return $"{target.TypeName} › {target.Label}";
        }

        if (type is { } linkType)
        {
            return PlanOutputLinkText.GetName(linkType);
        }

        var block = history.GetBlock(address, position);

        return block >= 0 && history.Store.Blocks[block].Address == address ? history.GetTypeName(block) : string.Empty;
    }

    private static string GetSubtitle(MemoryHistoryViewModel history, ulong address, TracePosition position)
    {
        var block = history.GetBlock(address, position);

        var text = $"0x{address:X16}";

        return block >= 0 && history.Store.Blocks[block].Address == address && history.GetTypeName(block) is { Length: > 0 } type
            ? $"{type} · {text}"
            : text;
    }

    private static string GetRowName(PlanOutputLink link, LineageBox source, int offset)
    {
        var name = link.Type == PlanOutputLinkType.RowContext ? SlotName : PlanOutputLinkText.GetName(link.Type);

        return source.Type switch
        {
            PlanOutputLinkType.Inputs
                or PlanOutputLinkType.OutputValues
                or PlanOutputLinkType.InputValues
                or PlanOutputLinkType.RowContext => $"{name} [{offset / PointerSize}]",
            PlanOutputLinkType.RowBuffer => $"{name} [{offset / EngineLayout.RowBuffer.EntryField.Length}]",
            PlanOutputLinkType.Selection => $"{name} [{offset / EngineLayout.Batch.SelectedRowField.Length}]",
            PlanOutputLinkType.Vectors => $"{name} [{offset / BatchReader.VectorEntrySize}]",
            PlanOutputLinkType.Vector => $"{name} [{offset / PointerSize}]",
            PlanOutputLinkType.ColumnMap => $"{name} [{(offset - VectorField.Offset) / VectorField.Length}]",
            _ => $"{name} +0x{offset:X}"
        };
    }

    private static string GetDetail(PlanOutputLink link) => link switch
    {
        { Type: PlanOutputLinkType.RowContext or PlanOutputLinkType.Cursor or PlanOutputLinkType.SelectedRow, Slot: >= 0 }
            => $"{link.Slot}",
        { Type: PlanOutputLinkType.VectorId or PlanOutputLinkType.Vector, Slot: >= 0 } => $"v{link.Slot}",
        { Type: PlanOutputLinkType.Reader or PlanOutputLinkType.Value, Slot: >= 0, Offset: { } offset }
            => $"Slot {link.Slot} · {PlanOutputLinkText.GetRowText(offset)}",
        { Type: PlanOutputLinkType.Reader or PlanOutputLinkType.Value, Slot: >= 0 } => $"Slot {link.Slot}",
        { Type: PlanOutputLinkType.BufferEntry or PlanOutputLinkType.Record, Offset: { } rowOffset }
            => PlanOutputLinkText.GetRowText(rowOffset),
        _ => string.Empty
    };
}
