using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.History.Enums;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using Microsoft.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.MemoryHistory;

public sealed partial class MemoryHistoryViewModel(Action<CallStackNode, int> openCall) : ObservableObject
{
    private const int OperatorWidth = 320;

    private const int ObjectWidth = 260;

    private const int AddressWidth = 170;

    private const int NumberWidth = 110;

    private const int PositionWidth = 120;

    private const int CallWidth = 80;

    private const int WriterWidth = 320;

    private const int HueWheel = 256;

    private const int RegionSaturation = 60;

    private const int RegionValue = 250;

    private const int OutlineSaturation = 210;

    private const int OutlineValue = 190;

    private const string IteratorObject = "Iterator";

    private const string UnknownWriter = "Unknown Writer";

    private const string SeveralWriters = "Several Writers";

    private const string Restored = "Restored";

    private const string UnknownObject = "Unknown Object";

    private static readonly Colour SharedColour = Colour.FromArgb(226, 226, 226);

    [ObservableProperty]
    private QueryResultSet? _blocksTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedBlockRow;

    [ObservableProperty]
    private QueryResultSet? _callsTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedCallRow;

    [ObservableProperty]
    private byte[]? _data;

    [ObservableProperty]
    private ObservableCollection<LogRecordAnnotation>? _changeSpans;

    [ObservableProperty]
    private LogRecordAnnotation? _selectedChangeSpan;

    [ObservableProperty]
    private ObservableCollection<Marker>? _markers;

    [ObservableProperty]
    private ObservableCollection<HexWatch>? _watches;

    [ObservableProperty]
    private IReadOnlyList<TraceMemoryBlock>? _memoryBlocks;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _objectDetail = string.Empty;

    [ObservableProperty]
    private bool _isAvailable;

    public MemoryHistoryStore Store => Trace?.MemoryHistory ?? MemoryHistoryStore.Empty;

    private Action<CallStackNode, int> OpenCall { get; } = openCall;

    private TraceResult? Trace { get; set; }

    private TraceTimeline? Window { get; set; }

    private IReadOnlyDictionary<ulong, IteratorTarget> Iterators { get; set; } = new Dictionary<ulong, IteratorTarget>();

    private int SelectedBlock { get; set; } = -1;

    private IReadOnlyList<MemoryHistoryCall> Calls { get; set; } = [];

    private Dictionary<CallStackNode, Colour> Colours { get; } = new(ReferenceEqualityComparer.Instance);

    private HexWatchStore WatchStore { get; } = new();

    private TracePosition WindowStart => Window?.Start.Position ?? default;

    private TracePosition WindowEnd => Window?.End.Position ?? TracePosition.Unknown;

    public void SetTrace(TraceResult? trace, IReadOnlyList<EngineEvent> events)
    {
        Trace = trace;

        IsAvailable = Store.Blocks.Count > 0;

        Iterators = trace is null ? new Dictionary<ulong, IteratorTarget>() : IteratorTarget.Build(trace.CallStack, events, trace.PlanLink);

        SetWindow(null);
    }

    public void SetWindow(TraceTimeline? window)
    {
        Window = window;

        MemoryBlocks = BuildMemoryBlocks();

        BlocksTable = BuildBlocksTable();

        SelectedBlockRow = BlocksTable.Rows.FirstOrDefault();

        if (SelectedBlockRow is null)
        {
            ShowBlock(-1);
        }
    }

    public void ShowCall(CallValue value) => OpenCall(value.Node, value.CallIndex);

    public void Select(int block, TracePosition? position)
    {
        if (BlocksTable?.Rows.FirstOrDefault(r => r.Id == block) is not { } row)
        {
            return;
        }

        SelectedBlockRow = row;

        if (position is not { } at
            || Trace is not { } trace
            || Store.GetWrites(block).FirstOrDefault(m => m.Position == at) is not { CallNodeIndex: >= 0 } write)
        {
            return;
        }

        var node = trace.Timeline.NodeOf(write.CallNodeIndex);

        for (var index = 0; index < Calls.Count; index++)
        {
            if (!Calls[index].IsAllocation && ReferenceEquals(Calls[index].Node, node) && Calls[index].CallIndex == write.CallIndex)
            {
                SelectedCallRow = CallsTable?.Rows.FirstOrDefault(r => r.Id == index);

                return;
            }
        }
    }

    public int GetBlock(ulong address, TracePosition position) => Store.GetBlock(address, position);

    public string GetObjectName(int block) => GetObjectLabel(Store.Blocks[block]) is { Length: > 0 } name ? name : UnknownObject;

    public CallStackNode? GetWriter(int callNodeIndex) => callNodeIndex >= 0 ? Trace?.Timeline.NodeOf(callNodeIndex) : null;

    public string GetWriterLabel(int callNodeIndex) => GetWriterLabel(GetWriter(callNodeIndex));

    public CallStackNode? GetAllocatingCall(int block)
    {
        if (Trace is not { } trace)
        {
            return null;
        }

        if (!Store.Blocks[block].IsInline)
        {
            return Store.GetAllocatingCall(block, trace.Timeline);
        }

        var writes = Store.GetWrites(block);

        return writes.Count > 0 ? trace.Timeline.NodeOf(writes[0].CallNodeIndex) : null;
    }

    public ObservableCollection<HexWatch> GetWatches(int block)
    {
        var label = GetObjectLabel(Store.Blocks[block]);

        var name = label.Length > 0 && label != IteratorObject ? label : $"0x{Store.Blocks[block].Address:X16}";

        return WatchStore.GetWatches(name);
    }

    partial void OnSelectedBlockRowChanged(ResultRow<long>? value)
        => ShowBlock(value is { Id: var index } && index < Store.Blocks.Count ? (int)index : -1);

    partial void OnSelectedCallRowChanged(ResultRow<long>? value)
    {
        if (SelectedBlock < 0 || value is not { Id: var index } || index >= Calls.Count)
        {
            Data = null;

            ChangeSpans = null;

            SelectedChangeSpan = null;

            return;
        }

        var call = Calls[(int)index];

        Data = Store.GetImage(SelectedBlock, call.LastWrite).Data;

        var label = GetWriterLabel(call.Node);

        ChangeSpans = new ObservableCollection<LogRecordAnnotation>(call.Regions.Select((r, i) => GetChangeSpan(call, r, i, label)));

        SelectedChangeSpan = ChangeSpans.FirstOrDefault(s => !s.IsDashed) ?? ChangeSpans.FirstOrDefault();
    }

    private QueryResultSet BuildBlocksTable()
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Operator", typeof(string), false) { Width = OperatorWidth },
            new(1, "Object", typeof(string), false) { Width = ObjectWidth },
            new(2, "Address", typeof(string), false) { Width = AddressWidth },
            new(3, "Size", typeof(int), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(4, "Calls", typeof(int), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(5, "Changes", typeof(int), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(6, "Changing Bytes", typeof(int), false) { Width = NumberWidth, Alignment = ResultAlignment.Right }
        };

        if (Trace is null || Window is null)
        {
            return new QueryResultSet { Columns = columns, Rows = [] };
        }

        int[] tracked = [.. Enumerable.Range(0, Store.Blocks.Count).Where(IsTracked)];

        var summaries = Store.GetSummaries(tracked, WindowStart, WindowEnd);

        var rows = tracked.Select((index, at) => (Block: Store.Blocks[index], Index: index, Summary: summaries[at]))
                        .Where(b => b.Summary.ChangeCount > 0)
                        .Select(b => (b.Block, b.Index, b.Summary, Operator: GetOperatorLabels(b.Summary.Operators)))
                        .OrderBy(b => Store.GetCreatedPosition(b.Index))
                        .ThenBy(b => b.Block.Address)
                        .Select(b => new ResultRow<long>([b.Operator,
                                                          GetObjectLabel(b.Block),
                                                          $"0x{b.Block.Address:X16}",
                                                          b.Block.Size,
                                                          b.Summary.CallCount,
                                                          b.Summary.ChangeCount,
                                                          b.Summary.ChangingByteCount])
                                     {
                                         Id = b.Index
                                     })
                        .ToArray();

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private List<TraceMemoryBlock> BuildMemoryBlocks()
    {
        var blocks = new List<TraceMemoryBlock>();

        if (Trace is null || Window is null)
        {
            return blocks;
        }

        for (var index = 0; index < Store.Blocks.Count; index++)
        {
            var block = Store.Blocks[index];

            if (!IsTracked(index))
            {
                continue;
            }

            var (first, count) = Store.GetChangedWriteRange(index, WindowStart, WindowEnd);

            if (count == 0)
            {
                continue;
            }

            var label = GetObjectLabel(block);

            blocks.Add(new TraceMemoryBlock(index,
                                            label.Length > 0 ? label : UnknownObject,
                                            block.Address,
                                            block.Size,
                                            block.From,
                                            block.To,
                                            new TraceMemoryWrites(Store, first, count)));
        }

        return blocks;
    }

    private bool IsTracked(int block) => Store.IsCreatedWithin(block, WindowStart, WindowEnd);

    private void ShowBlock(int block)
    {
        SelectedBlock = block;

        ObjectName = block >= 0 ? GetObjectName(block) : string.Empty;

        ObjectDetail = block >= 0 ? $"0x{Store.Blocks[block].Address:X16}, {Store.Blocks[block].Size} Bytes" : string.Empty;

        Calls = block >= 0 && Trace is { } trace ? Store.GetCalls(block, trace.Timeline, WindowStart, WindowEnd) : [];

        SetColours();

        Markers = block >= 0 ? new ObservableCollection<Marker>(GetRegionMarkers()) : null;

        Watches = block >= 0 ? GetWatches(block) : null;

        CallsTable = BuildCallsTable();

        SelectedCallRow = CallsTable.Rows.FirstOrDefault();

        if (SelectedCallRow is null)
        {
            OnSelectedCallRowChanged(null);
        }
    }

    private void SetColours()
    {
        Colours.Clear();

        var writers = Calls.Select(c => c.Node).OfType<CallStackNode>().Distinct().ToList();

        for (var index = 0; index < writers.Count; index++)
        {
            Colours[writers[index]] = GetColour(index, writers.Count);
        }
    }

    private QueryResultSet BuildCallsTable()
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Position", typeof(string), false) { Width = PositionWidth, Alignment = ResultAlignment.Right },
            new(1, "Writer", typeof(string), false) { Width = WriterWidth },
            new(2, "Operator", typeof(string), false) { Width = OperatorWidth },
            new(3, "Call", typeof(long), false) { Width = CallWidth, Alignment = ResultAlignment.Right },
            new(4, "Changes", typeof(int), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(5, "Changed Bytes", typeof(int), false) { Width = NumberWidth, Alignment = ResultAlignment.Right }
        };

        return new QueryResultSet { Columns = columns, Rows = [.. Calls.Select(GetCallRow)] };
    }

    private ResultRow<long> GetCallRow(MemoryHistoryCall call, int index)
        => new([TracePositionText.GetRulerText(call.Position, WindowStart.Sequence),
                new ColouredText(GetWriterLabel(call.Node), GetColour(call.Node)),
                GetOperatorLabel(call.Iterator),
                call.Node is { } node ? new CallValue(node, call.CallIndex) : null,
                call.ChangeCount,
                call.NetByteCount])
        {
            Id = index
        };

    private IEnumerable<Marker> GetRegionMarkers()
    {
        var boundaries = new List<(int Offset, CallStackNode? Writer, int Step)>();

        foreach (var writer in Calls.Where(c => !c.IsAllocation).GroupBy(c => c.Node))
        {
            foreach (var region in Merge(writer.SelectMany(c => c.Regions)))
            {
                boundaries.Add((region.Offset, writer.Key, 1));

                boundaries.Add((region.Offset + region.Length, writer.Key, -1));
            }
        }

        var active = new List<CallStackNode?>();

        var start = 0;

        foreach (var group in boundaries.GroupBy(b => b.Offset).OrderBy(g => g.Key))
        {
            if (active.Count > 0 && group.Key > start)
            {
                yield return GetRegionMarker(start, group.Key, active);
            }

            foreach (var boundary in group)
            {
                if (boundary.Step > 0)
                {
                    active.Add(boundary.Writer);
                }
                else
                {
                    active.Remove(boundary.Writer);
                }
            }

            start = group.Key;
        }
    }

    private Marker GetRegionMarker(int start, int end, List<CallStackNode?> writers)
    {
        var isShared = writers.Count > 1;

        return new Marker
        {
            Name = isShared ? $"{SeveralWriters}: {string.Join(", ", writers.Select(GetWriterLabel))}" : GetWriterLabel(writers[0]),
            StartPosition = start,
            EndPosition = end - 1,
            BackColour = (isShared ? SharedColour : GetColour(writers[0])).ToWindowsColor(),
            ForeColour = Colors.Black
        };
    }

    private static IEnumerable<MemoryHistoryRegion> Merge(IEnumerable<MemoryHistoryRegion> regions)
    {
        MemoryHistoryRegion? current = null;

        foreach (var region in regions.OrderBy(r => r.Offset))
        {
            if (current is { } last && last.Offset + last.Length >= region.Offset)
            {
                current = last with { Length = Math.Max(last.Offset + last.Length, region.Offset + region.Length) - last.Offset };

                continue;
            }

            if (current is { } completed)
            {
                yield return completed;
            }

            current = region;
        }

        if (current is { } final)
        {
            yield return final;
        }
    }

    private Colour GetColour(CallStackNode? writer)
        => writer is not null && Colours.TryGetValue(writer, out var colour) ? colour : SharedColour;

    private static Colour GetColour(int index, int count)
        => ColourHelpers.HsvToColor(index * HueWheel / Math.Max(count, 1) % HueWheel, RegionSaturation, RegionValue);

    private static LogRecordAnnotation GetChangeSpan(MemoryHistoryCall call, MemoryHistoryRegion region, int index, string label)
    {
        var isRestored = !call.NetRegions.Any(n => n.Offset < region.Offset + region.Length && region.Offset < n.Offset + n.Length);

        var hue = index * HueWheel / Math.Max(call.Regions.Count, 1) % HueWheel;

        return new LogRecordAnnotation
        {
            Offset = region.Offset,
            Length = region.Length,
            Description = isRestored ? $"{label} ({Restored})" : label,
            OutlineColour = ColourHelpers.HsvToColor(hue, OutlineSaturation, OutlineValue).ToWindowsColor(),
            IsDashed = isRestored
        };
    }

    private string GetOperatorLabel(ulong iterator)
    {
        if (iterator == 0)
        {
            return string.Empty;
        }

        return Iterators.TryGetValue(iterator, out var target) ? target.Label : $"0x{iterator:X}";
    }

    private string GetOperatorLabels(IEnumerable<ulong> iterators) => string.Join(", ", iterators.Select(GetOperatorLabel));

    private string GetObjectLabel(MemoryHistoryBlock block)
        => Trace?.MemoryMap.GetLabel(block.Address, block.FromPosition, block.ToPosition)
           ?? (block.IsIterator ? IteratorObject : string.Empty);

    private static string GetWriterLabel(CallStackNode? writer) => writer?.Symbol ?? UnknownWriter;

}
