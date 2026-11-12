using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.Allocation;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
using InternalsViewer.Query.FullTrace.Timeline;
using InternalsViewer.Query.FullTrace.Timeline.Enums;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline.Enums;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Microsoft.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public abstract partial class MemoryStructureViewModel : ObservableObject
{
    protected const int PageSize = 8192;

    protected const int FieldWidth = 200;

    protected const int OffsetWidth = 80;

    protected const int AddressWidth = 160;

    private const string NotAllocated = "Not Allocated At This Position";

    private const string PointerPrefix = "Pointer: ";

    private const string OpenMemoryCommand = "Open Memory";

    private const string OpenPageCommand = "Open Page";

    private const string SelectCommand = "Select";

    private const int HueWheel = 256;

    private const double GoldenRatio = 0.6180339887;

    private const int FieldSaturation = 60;

    private const int FieldValue = 250;

    private const int ValueWidth = 360;

    private const int NumberWidth = 110;

    private const int ChartBuckets = 400;

    protected static readonly TimeSpan LoadInterval = TimeSpan.FromMilliseconds(50);

    [ObservableProperty]
    private List<MemoryStructureNode> _roots = [];

    [ObservableProperty]
    private MemoryStructureNode? _selectedNode;

    [ObservableProperty]
    private List<ActionLink> _path = [];

    [ObservableProperty]
    private string _pathTitle = string.Empty;

    [ObservableProperty]
    private byte[]? _data;

    [ObservableProperty]
    private ObservableCollection<Marker>? _markers;

    [ObservableProperty]
    private Marker? _selectedMarker;

    [ObservableProperty]
    private int? _scrollToOffset;

    [ObservableProperty]
    private LogRecordAnnotation? _itemOutline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFieldTableVisible))]
    private QueryResultSet? _fieldTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedFieldRow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEntryTableVisible))]
    private QueryResultSet? _entryTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedEntryRow;

    [ObservableProperty]
    private bool _isPageVisible;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private List<HexLink>? _links;

    [ObservableProperty]
    private string _blockAddress = string.Empty;

    [ObservableProperty]
    private string _blockType = string.Empty;

    [ObservableProperty]
    private QueryResultSet? _memoryTable;

    [ObservableProperty]
    private MemoryUsageChart? _memoryChart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHexVisible))]
    private bool _isMemoryVisible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoBackCommand))]
    private bool _canGoBack;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoForwardCommand))]
    private bool _canGoForward;

    protected MemoryStructureViewModel(MemoryHistoryViewModel history, TracePosition position, ITraceNavigator navigator)
    {
        History = history;

        OpenedAt = position;

        Navigator = navigator;

        Page = new MemoryPageViewModel(ShowPageMarkers);

        EntryDetails = GetEntryDetails;
    }

    public abstract string Title { get; }

    public abstract string Key { get; }

    public MemoryPageViewModel Page { get; }

    public Func<ResultRow<long>, ResultDetails> EntryDetails { get; }

    public bool IsFieldTableVisible => FieldTable is not null;

    public bool IsEntryTableVisible => EntryTable is not null;

    public bool IsHexVisible => !IsMemoryVisible;

    protected MemoryHistoryViewModel History { get; }

    protected ITraceNavigator Navigator { get; }

    protected TracePosition OpenedAt { get; }

    protected TracePosition Position => Playhead ?? OpenedAt;

    protected abstract HexImage? Image { get; }

    protected string StructureKey { get; set; } = string.Empty;

    protected string SelectedKey { get; set; } = string.Empty;

    protected Dictionary<ulong, MemoryStructureNode> Nodes { get; set; } = [];

    protected ulong HexAddress { get; private set; }

    protected ulong? SelectedItem { get; set; }

    protected int? PendingSlotOffset { get; set; }

    protected string? TableKey { get; set; }

    protected List<(MemoryField Field, int Colour)> ShownFields { get; set; } = [];

    protected int ShownBase { get; set; }

    protected bool IsApplying { get; set; }

    protected Marker? MaskedMarker { get; private set; }

    private TracePosition? Playhead { get; set; }

    private int PageBlock { get; set; } = -1;

    private string? MarkerKey { get; set; }

    private bool IsSelecting { get; set; }

    private bool IsSelectingRow { get; set; }

    private ulong? OutlinedItem { get; set; }

    private List<HistoryEntry> NavigationHistory { get; } = [];

    private int HistoryIndex { get; set; } = -1;

    private bool IsReplaying { get; set; }

    private (IReadOnlyList<TraceMemoryUsage> Usage, List<MemoryUsageSeries> Series)? ChartSeries { get; set; }

    private HistoryEntry CurrentEntry
        => new(SelectedKey, SelectedItem, Find(SelectedKey) is { IsPage: true } ? Page.SelectedSlot?.Offset : null);

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        Load();
    }

    public void MovePlayhead(double fraction)
    {
        if (History.QueryTimeline is not { } timeline)
        {
            return;
        }

        var start = timeline.GetStart(TraceTimelineAxis.Position);

        var end = timeline.GetEnd(TraceTimelineAxis.Position);

        var position = timeline.Scale.GetPosition(start + Math.Clamp(fraction, 0, 1) * (end - start));

        Navigator.MovePlayhead(timeline.GetPoint(position));
    }

    public abstract void SelectOffset(int offset);

    public virtual void Close()
    {
        Page.Clear();

        Nodes = [];
    }

    protected static List<Colour> CreateColours(int count)
        => [.. Enumerable.Range(0, count).Select(i => ColourHelpers.HsvToColor((int)(i * GoldenRatio * HueWheel) % HueWheel,
                                                                               FieldSaturation,
                                                                               FieldValue))];

    protected static ulong? ReadValue(byte[]? data, int offset, int length)
    {
        if (data is null || offset < 0 || offset + length > data.Length)
        {
            return null;
        }

        var span = data.AsSpan(offset, length);

        return length switch
        {
            sizeof(byte) => span[0],
            sizeof(ushort) => BinaryPrimitives.ReadUInt16LittleEndian(span),
            sizeof(uint) => BinaryPrimitives.ReadUInt32LittleEndian(span),
            _ => BinaryPrimitives.ReadUInt64LittleEndian(span)
        };
    }

    protected static Marker CreateMarker(string name, int offset, int length, Colour colour)
        => new()
        {
            Name = name,
            StartPosition = offset,
            EndPosition = offset + length - 1,
            BackColour = colour.ToWindowsColor(),
            MapColour = colour.ToWindowsColor(),
            ForeColour = Colors.Black
        };

    protected static IEnumerable<MemoryStructureNode> Flatten(MemoryStructureNode node) => node.Children.SelectMany(Flatten).Prepend(node);

    protected static string GetAddressText(ulong address) => $"0x{address:X16}";

    protected abstract void Load();

    protected abstract void ShowDetail(MemoryStructureNode node);

    protected abstract ResultDetails GetEntryDetails(ResultRow<long> row);

    protected abstract MemoryStructureNode? GetPointerNode(ulong value);

    protected abstract void OpenPointer(MemoryStructureNode node, ulong value);

    protected virtual ulong GetHexAddress(MemoryStructureNode node) => node.HasData ? node.Address : 0;

    protected MemoryStructureNode? Find(string key) => Roots.SelectMany(Flatten).FirstOrDefault(n => n.Key == key);

    protected void RememberFirst()
    {
        if (HistoryIndex < 0)
        {
            Remember(CurrentEntry);
        }
    }

    protected void Select(MemoryStructureNode node, ulong? item = null)
    {
        var target = Find(node.Key) ?? node;

        Remember(new HistoryEntry(target.Key, item ?? (target.Key == SelectedKey ? SelectedItem : null), PendingSlotOffset));

        if (target.Key != SelectedKey || IsReplaying)
        {
            SelectedKey = target.Key;

            SelectedItem = null;

            SelectedFieldRow = null;

            SelectedEntryRow = null;

            Mask(null);

            ClearOutline();
        }

        if (item is not null)
        {
            SelectedItem = item;
        }

        IsSelecting = true;

        SelectedNode = target;

        IsSelecting = false;

        Show();
    }

    protected void Show()
    {
        if (SelectedNode is not { } node)
        {
            return;
        }

        HexAddress = GetHexAddress(node);

        IsMemoryVisible = node.Type == MemoryStructureNodeType.Memory;

        ShowPath(node);

        if (Image is { } image && image.Address == HexAddress)
        {
            ShowImage(image, node);

            ShowDetail(node);
        }
        else
        {
            Load();
        }
    }

    protected void ClearDetail()
    {
        Data = null;

        Markers = null;

        MarkerKey = null;

        TableKey = null;

        FieldTable = null;

        EntryTable = null;

        IsPageVisible = false;

        ShownFields = [];

        ClearOutline();
    }

    protected void ShowFields(MemoryStructureNode node, IReadOnlyList<MemoryField> layout, IReadOnlyList<Colour> colours)
    {
        EntryTable = null;

        IsPageVisible = false;

        ClearOutline();

        var data = Image?.Data;

        ShownBase = Image?.Offset ?? 0;

        ShownFields = [.. layout.Select((f, i) => (f, i))];

        var layoutKey = string.Join(",", layout.Select(f => f.Offset));

        SetMarkers($"{node.Key} {layoutKey}",
                   () => ShownFields.Select(f => CreateMarker(f.Field.Name,
                                                              ShownBase + f.Field.Offset,
                                                              f.Field.Length,
                                                              colours[f.Colour])));

        var values = ShownFields.Select(f => ReadValue(data, ShownBase + f.Field.Offset, f.Field.Length)).ToList();

        var key = $"{node.Key} {layoutKey} {StructureKey.GetHashCode()} {string.Join(",", values)}";

        if (key == TableKey && FieldTable is not null)
        {
            return;
        }

        TableKey = key;

        var selected = SelectedFieldRow?.Id;

        var columns = new List<ResultColumn>
        {
            new(0, "Field", typeof(string), false) { Width = FieldWidth },
            new(1, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
            new(2, "Value", typeof(string), true) { Width = ValueWidth }
        };

        var rows = ShownFields.Select((f, i) => new ResultRow<long>([new SwatchText(f.Field.Name, colours[f.Colour]),
                                                                     $"0x{f.Field.Offset:X}",
                                                                     GetValueCell(f.Field, values[i])])
        {
            Id = i
        }).ToList();

        FieldTable = new QueryResultSet { Columns = columns, Rows = rows };

        SelectedFieldRow = rows.FirstOrDefault(r => r.Id == selected);
    }

    protected object GetPointerCell(ulong pointer)
    {
        var text = GetAddressText(pointer);

        if (pointer == 0)
        {
            return text;
        }

        if (GetPointerNode(pointer) is { } node)
        {
            return new ActionLinks([new ActionLink(text, () => OpenPointer(node, pointer))]) { Suffix = node.Name };
        }

        var position = Position;

        var block = History.GetBlock(pointer, position);

        if (block < 0)
        {
            return text;
        }

        return new ActionLinks([new ActionLink(text, () => Navigator.OpenMemory(block, pointer, position))])
        {
            Suffix = History.GetPointerTarget(pointer, position)
        };
    }

    protected void SelectItemRow()
    {
        if (SelectedItem is { } selected
            && EntryTable?.Rows.FirstOrDefault(r => r.Id == (long)selected) is { } row
            && !ReferenceEquals(row, SelectedEntryRow))
        {
            IsSelectingRow = true;

            SelectedEntryRow = row;

            IsSelectingRow = false;
        }
    }

    protected void SelectFieldAt(int offset)
    {
        var field = ShownFields.FindIndex(f => offset >= ShownBase + f.Field.Offset
                                               && offset < ShownBase + f.Field.Offset + f.Field.Length);

        if (FieldTable?.Rows.FirstOrDefault(r => r.Id == field) is { } row)
        {
            SelectedFieldRow = row;
        }
    }

    protected void MaskAt(int offset, Marker? previous)
    {
        var marker = MarkerLookup.FindAt(Markers, offset);

        Mask(marker is not null && !IsSameMarker(marker, previous) ? marker : null);
    }

    protected void Mask(Marker? marker)
    {
        MaskedMarker = marker;

        SelectedMarker = marker;
    }

    protected void ShowPage(RowRecordLayout? layout)
    {
        FieldTable = null;

        EntryTable = null;

        TableKey = null;

        ShownFields = [];

        IsPageVisible = true;

        ClearOutline();

        var block = Image?.Block ?? -1;

        if (block != PageBlock)
        {
            PageBlock = block;

            if (block >= 0)
            {
                Page.Load(History, block, layout);
            }
            else
            {
                Page.Clear();
            }
        }

        Page.SetData(Data);

        Page.Refresh();

        if (PendingSlotOffset is { } offset)
        {
            PendingSlotOffset = null;

            Page.SelectOffset(offset);
        }

        MarkerKey = null;

        Markers = Page.Markers;
    }

    protected void ShowMemory(Task<List<TraceMemoryUsage>> usage)
    {
        if (usage.IsCompleted)
        {
            ShowMemory(usage.IsCompletedSuccessfully ? usage.Result : []);

            return;
        }

        ShowMemory([]);

        _ = ShowMemoryWhenReady(usage);
    }

    protected void ShowMemory(IReadOnlyList<TraceMemoryUsage> usage)
    {
        FieldTable = null;

        EntryTable = null;

        IsPageVisible = false;

        ShownFields = [];

        ClearOutline();

        var colours = CreateColours(usage.Count);

        var position = Position;

        var columns = new List<ResultColumn>
        {
            new(0, "Type", typeof(string), false) { Width = FieldWidth },
            new(1, "Allocations", typeof(string), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(2, "Allocated", typeof(string), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(3, "In Use", typeof(string), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(4, "Peak In Use", typeof(string), false) { Width = NumberWidth, Alignment = ResultAlignment.Right }
        };

        var rows = usage.Select((u, i) => new ResultRow<long>([u.Name,
                                                               u.Allocations.ToString("N0", CultureInfo.CurrentCulture),
                                                               FormatSize(u.AllocatedBytes),
                                                               FormatSize(u.InUse.GetValue(position)),
                                                               FormatSize(u.PeakBytes)])
        {
            Id = i
        }).ToList();

        MemoryTable = new QueryResultSet { Columns = columns, Rows = rows };

        MemoryChart = CreateChart(usage, colours, position);
    }

    protected void SetMarkers(string key, Func<IEnumerable<Marker>> create)
    {
        if (key == MarkerKey && Markers is not null)
        {
            return;
        }

        MarkerKey = key;

        Markers = new ObservableCollection<Marker>(create());
    }

    protected void ShowItemMarkers(string key, Func<IEnumerable<Marker>> create, int size)
    {
        if (GetSelectedItem() is not { } selected)
        {
            SetMarkers(key, create);

            ClearOutline();

            return;
        }

        var (item, offset) = selected;

        SetMarkers($"{key} {offset}", () => create().Where(m => m.StartPosition >= offset && m.EndPosition < offset + size));

        if (OutlinedItem == item)
        {
            return;
        }

        OutlinedItem = item;

        ItemOutline = new LogRecordAnnotation
        {
            Offset = offset,
            Length = size,
            Description = GetAddressText(item),
            OutlineColour = Colors.Red
        };
    }

    private static string FormatSize(ulong bytes) => StringHelpers.FormatSize((long)bytes);

    private static ulong[] Sample(MemoryUsageCurve curve, TraceTimeline timeline, double start, double end)
    {
        var values = new ulong[ChartBuckets];

        var step = (end - start) / ChartBuckets;

        for (var bucket = 0; bucket < ChartBuckets; bucket++)
        {
            var from = timeline.Scale.GetPosition(start + bucket * step);

            var to = timeline.Scale.GetPosition(start + (bucket + 1) * step);

            values[bucket] = curve.GetPeakDuring(from, to);
        }

        return values;
    }

    private static bool IsSameMarker(Marker marker, Marker? other)
        => other is not null
           && marker.StartPosition == other.StartPosition
           && marker.EndPosition == other.EndPosition
           && marker.Name == other.Name;

    partial void OnSelectedNodeChanged(MemoryStructureNode? value)
    {
        if (value is { IsSelectable: true } && !IsApplying && !IsSelecting)
        {
            Select(value);
        }
    }

    partial void OnSelectedFieldRowChanged(ResultRow<long>? value)
    {
        if (value is not { Id: var id } || id < 0 || id >= ShownFields.Count)
        {
            return;
        }

        var field = ShownFields[(int)id].Field;

        Mask(Markers?.FirstOrDefault(m => m.StartPosition == ShownBase + field.Offset && m.Name == field.Name));
    }

    partial void OnSelectedEntryRowChanged(ResultRow<long>? value)
    {
        if (value is not { Id: var id } || IsSelectingRow)
        {
            return;
        }

        SelectedItem = (ulong)id;

        Show();
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack() => Travel(-1);

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private void GoForward() => Travel(1);

    private void Remember(HistoryEntry entry)
    {
        if (IsReplaying)
        {
            return;
        }

        if (HistoryIndex >= 0)
        {
            NavigationHistory[HistoryIndex] = CurrentEntry;

            if (NavigationHistory[HistoryIndex] == entry)
            {
                return;
            }

            NavigationHistory.RemoveRange(HistoryIndex + 1, NavigationHistory.Count - HistoryIndex - 1);
        }

        NavigationHistory.Add(entry);

        HistoryIndex = NavigationHistory.Count - 1;

        UpdateHistory();
    }

    private void Travel(int step)
    {
        if (HistoryIndex < 0)
        {
            return;
        }

        NavigationHistory[HistoryIndex] = CurrentEntry;

        for (var index = HistoryIndex + step; index >= 0 && index < NavigationHistory.Count; index += step)
        {
            var entry = NavigationHistory[index];

            if (Find(entry.Key) is not { } node)
            {
                continue;
            }

            HistoryIndex = index;

            IsReplaying = true;

            PendingSlotOffset = entry.SlotOffset;

            Select(node, entry.Item);

            IsReplaying = false;

            break;
        }

        UpdateHistory();
    }

    private void UpdateHistory()
    {
        CanGoBack = HistoryIndex > 0;

        CanGoForward = HistoryIndex >= 0 && HistoryIndex < NavigationHistory.Count - 1;
    }

    private (ulong Item, int Offset)? GetSelectedItem()
    {
        if (SelectedItem is not { } item
            || Image is not { } image
            || item < HexAddress
            || item >= HexAddress + PageSize
            || EntryTable?.Rows.Any(r => r.Id == (long)item) != true)
        {
            return null;
        }

        return (item, image.Offset + (int)(item - HexAddress));
    }

    private void ClearOutline()
    {
        OutlinedItem = null;

        ItemOutline = null;
    }

    private void ShowPath(MemoryStructureNode node)
    {
        var chain = new List<MemoryStructureNode>();

        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current.IsSelectable)
            {
                chain.Insert(0, current);
            }
        }

        Path = [.. chain.Select(n => new ActionLink(n.Name, () => Select(n)))];

        PathTitle = node.Name;
    }

    private void ShowImage(HexImage image, MemoryStructureNode node)
    {
        if (Data is null || image.Data is null || !image.Data.AsSpan().SequenceEqual(Data))
        {
            Data = image.Data;
        }

        Links = GetLinks(image);

        BlockAddress = image.Block >= 0 ? GetAddressText(History.Store.Blocks[image.Block].Address) : string.Empty;

        BlockType = image.Block >= 0 ? History.GetTypeName(image.Block) : string.Empty;

        EmptyText = node.HasData && image.Data is null ? NotAllocated : string.Empty;
    }

    private List<HexLink> GetLinks(HexImage image)
    {
        var links = new List<HexLink>();

        var position = Position;

        var size = MemoryHistoryPointerReader.PointerSize;

        foreach (var (offset, value, block, page) in image.Pointers)
        {
            if (GetPointerNode(value) is { } node)
            {
                links.Add(new HexLink(offset, size, PointerPrefix + node.Name, SelectCommand, () => OpenPointer(node, value)));
            }
            else if (block >= 0)
            {
                var target = History.GetPointerTarget(value, position) ?? GetAddressText(value);

                links.Add(new HexLink(offset,
                                      size,
                                      PointerPrefix + target,
                                      OpenMemoryCommand,
                                      () => Navigator.OpenMemory(block, value, position)));
            }
            else if (page is { } reference && History.IsQueryDatabase(reference))
            {
                var label = History.GetBufferPageLabel(reference);

                links.Add(new HexLink(offset, size, PointerPrefix + label, OpenPageCommand, () => Navigator.OpenBufferPage(reference)));
            }
        }

        return links;
    }

    private object? GetValueCell(MemoryField field, ulong? value)
    {
        if (value is not { } found)
        {
            return null;
        }

        if (field.IsPointer)
        {
            return GetPointerCell(found);
        }

        return field.IsHex ? $"0x{found:X8}" : found.ToString("N0", CultureInfo.CurrentCulture);
    }

    private MemoryUsageChart? CreateChart(IReadOnlyList<TraceMemoryUsage> usage, IReadOnlyList<Colour> colours, TracePosition position)
    {
        if (usage.Count == 0 || History.QueryTimeline is not { } timeline)
        {
            return null;
        }

        var start = timeline.GetStart(TraceTimelineAxis.Position);

        var end = timeline.GetEnd(TraceTimelineAxis.Position);

        if (end <= start)
        {
            return null;
        }

        if (ChartSeries is not { } cached || !ReferenceEquals(cached.Usage, usage))
        {
            List<MemoryUsageSeries> series =
            [
                .. usage.Select((u, i) => new MemoryUsageSeries(u.Name, colours[i], Sample(u.InUse, timeline, start, end), i == 0))
            ];

            cached = (usage, series);

            ChartSeries = cached;
        }

        return new MemoryUsageChart(cached.Series, usage[0].PeakBytes, (timeline.Scale.GetValue(position) - start) / (end - start));
    }

    private async Task ShowMemoryWhenReady(Task usage)
    {
        await Task.WhenAny(usage);

        if (SelectedNode is { Type: MemoryStructureNodeType.Memory })
        {
            Show();
        }
    }

    private void ShowPageMarkers(int? offset)
    {
        if (SelectedNode is not { IsPage: true })
        {
            return;
        }

        Markers = Page.Markers;

        if (offset is { } scroll)
        {
            ScrollToOffset = scroll;
        }
    }

    protected sealed record HexImage(ulong Address, int Block, int Offset, byte[]? Data, List<MemoryHistoryPointer> Pointers)
    {
        public static HexImage Read(MemoryHistoryViewModel history, ulong address, TracePosition position)
        {
            var store = history.Store;

            var block = address == 0 ? -1 : store.GetBlock(address, position);

            var count = block >= 0 ? store.GetWriteCount(block, position) : 0;

            var image = count > 0 ? store.GetImage(block, count - 1) : null;

            var pointers = image is not null
                ? MemoryHistoryPointerReader.Find(store, history.BufferPool, block, image, count, position)
                : [];

            var offset = block >= 0 ? (int)(address - store.Blocks[block].Address) : 0;

            return new HexImage(address, block, offset, image?.Data, pointers);
        }
    }

    private readonly record struct HistoryEntry(string Key, ulong? Item, int? SlotOffset);
}
