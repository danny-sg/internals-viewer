using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory.Enums;
using Microsoft.UI;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class PlayheadMemoryViewModel : ObservableObject
{
    private const string NotYetChanged = "Not Yet Changed";

    private const int HistoryTab = 1;

    private const int BufferTab = 4;

    private const int BatchTab = 5;

    private const int LoadInterval = 50;

    private const int VectorsTab = 6;

    private const int PageTab = 7;

    private const string OpenMemoryCommand = "Open Memory";

    private const string OpenPageCommand = "Open Page";

    private const string PointerPrefix = "Pointer: ";

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private byte[]? _data;

    [ObservableProperty]
    private ObservableCollection<Marker>? _markers;

    [ObservableProperty]
    private IReadOnlyList<HexLink>? _links;

    [ObservableProperty]
    private ObservableCollection<HexWatch>? _watches;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private LogRecordAnnotation? _selectedChangeSpan;

    [ObservableProperty]
    private int? _scrollToOffset;

    public PlayheadMemoryViewModel(MemoryHistoryViewModel history)
    {
        History = history;

        Writes = new MemoryWritesViewModel(this);

        References = new MemoryReferencesViewModel(this);

        HexLoader = new PlayheadLoader<HexImage?>(TimeSpan.FromMilliseconds(LoadInterval), ApplyHex);
    }

    public event EventHandler<TracePoint>? PlayheadRequested;

    public event EventHandler<MemoryRequest>? MemoryRequested;

    public MemoryHistoryViewModel History { get; }

    public MemoryDetailsViewModel Details { get; } = new();

    public MemoryWritesViewModel Writes { get; }

    public MemoryWatchesViewModel WatchList { get; } = new();

    public MemoryReferencesViewModel References { get; }

    public MemoryBufferViewModel Buffer { get; } = new();

    public MemoryBatchViewModel Batch { get; } = new();

    public MemoryPageViewModel WorkfilePage { get; } = new();

    public int Block { get; private set; } = -1;

    public TracePosition Position => Playhead ?? OpenedAt;

    public string Title => IsOpen && History.IsPage(Block) ? $"{ObjectName} 0x{History.Store.Blocks[Block].Address:X16}" : ObjectName;

    public IPlayheadChart? Chart { get; set; }

    public ITraceNavigator? Navigator { get; set; }

    private TracePosition OpenedAt { get; set; } = TracePosition.Unknown;

    private TracePosition? Playhead { get; set; }

    private int WriteCount { get; set; } = -1;

    private PlayheadLoader<HexImage?> HexLoader { get; }

    private List<MemoryChangeRun> Runs { get; set; } = [];

    private List<MemoryHistoryPointer> Pointers { get; set; } = [];

    private ObservableCollection<Marker>? WriterMarkers { get; set; }

    public void Open(int block, ulong address, TracePosition position)
    {
        if (block < 0 || block >= History.Store.Blocks.Count)
        {
            return;
        }

        Block = block;

        OpenedAt = position;

        WriteCount = -1;

        Links = null;

        References.Clear();

        ObjectName = History.GetObjectName(block);

        Watches = History.GetWatches(block);

        Watches.CollectionChanged += OnWatchesChanged;

        IsOpen = true;

        SelectedChangeSpan = null;

        Refresh();

        Details.Load(this);

        Writes.Load();

        WatchList.Attach(Watches);

        Buffer.Load(this);

        Batch.Load(this);

        WorkfilePage.Load(this);

        ApplyMarkers();

        if ((SelectedTab == BufferTab && !Buffer.IsAvailable)
            || (SelectedTab == BatchTab && !Batch.IsAvailable)
            || (SelectedTab == VectorsTab && !Batch.IsVectorsAvailable)
            || (SelectedTab == PageTab && !WorkfilePage.IsAvailable))
        {
            SelectedTab = 0;
        }

        RefreshBatch();

        ShowAddress(address);
    }

    public void ShowAddress(ulong address, int length = 1)
    {
        if (!IsOpen)
        {
            return;
        }

        var block = History.Store.Blocks[Block];

        var offset = address - block.Address;

        SelectedChangeSpan = length > 1 && offset < (ulong)block.Size
            ? new LogRecordAnnotation { Offset = (int)offset, Length = length, Description = $"0x{address:X16}" }
            : null;
    }

    public void Close()
    {
        HexLoader.Cancel();

        WatchList.Attach(null);

        References.Clear();

        Buffer.Clear();

        Batch.Clear();

        WorkfilePage.Clear();

        Writes.Clear();

        Details.Clear();

        Block = -1;

        Data = null;

        WriterMarkers = null;

        Markers = null;

        Pointers = [];

        Links = null;

        if (Watches is { } watches)
        {
            watches.CollectionChanged -= OnWatchesChanged;
        }

        Watches = null;

        SelectedChangeSpan = null;

        EmptyText = string.Empty;

        IsOpen = false;
    }

    public void RequestPlayhead(TracePoint point) => PlayheadRequested?.Invoke(this, point);

    public TraceMemoryWrites? GetUpdates()
    {
        if (!IsOpen)
        {
            return null;
        }

        var block = History.Store.Blocks[Block];

        return new TraceMemoryWrites(History.Store, block.FirstWrite, block.WriteCount);
    }

    public void RequestMemory(ulong address, int length = 1, TracePosition? position = null)
        => MemoryRequested?.Invoke(this, new MemoryRequest(address, length, position));

    public object? GetBufferPageCell(string text, ulong address, TracePosition position)
    {
        if (History.GetBufferPage(address, position) is not { } page)
        {
            return null;
        }

        var label = History.GetBufferPageLabel(page);

        return History.IsQueryDatabase(page)
            ? new ActionLinks([new ActionLink(text, () => Navigator?.OpenBufferPage(page))]) { Suffix = label }
            : $"{text}  {label}";
    }

    public void GoToWrite(int index)
    {
        if (IsOpen && index >= 0 && index < History.Store.Blocks[Block].WriteCount)
        {
            RequestPlayhead(History.Store.GetPoint(History.Store.Blocks[Block].FirstWrite + index));
        }
    }

    public void StepWrite(bool isForward) => Writes.Step(isForward, Playhead ?? OpenedAt);

    public void SelectWrite(int index)
    {
        if (!IsOpen || index < 0 || index >= History.Store.Blocks[Block].WriteCount)
        {
            return;
        }

        var write = History.Store.GetWrite(History.Store.Blocks[Block].FirstWrite + index);

        GoToWrite(index);

        SelectedChangeSpan = new LogRecordAnnotation
        {
            Offset = write.Offset,
            Length = write.Length,
            Description = History.GetWriterLabel(write.CallNodeIndex),
            OutlineColour = GetCategoryColour(write.CallNodeIndex)
        };
    }

    public void PinRange(int offset, int length)
    {
        if (Watches is not { } watches || length <= 0)
        {
            return;
        }

        foreach (var watch in watches)
        {
            if (watch.Offset == offset && watch.Length == length && watch.DataType is null)
            {
                return;
            }
        }

        watches.Add(new HexWatch(offset, length));
    }

    public void ShowHistory(string? function, ulong? address)
    {
        Writes.SetFilters(function, address);

        SelectedTab = HistoryTab;
    }

    public void ClearWriteSelection() => SelectedChangeSpan = null;

    public void ShowHistoryAt(int offset)
    {
        if (!IsOpen || offset < 0 || offset >= History.Store.Blocks[Block].Size)
        {
            return;
        }

        SelectedChangeSpan = null;

        string? function = null;

        foreach (var run in Runs)
        {
            if (run.Start <= offset && offset <= run.End)
            {
                function = run.State == MemoryChangeState.Pending ? null : History.GetWriterLabel(run.CallNodeIndex);

                break;
            }
        }

        ShowHistory(function, History.Store.Blocks[Block].Address + (ulong)offset);
    }

    public void MarkPosition(TracePoint? point) => Chart?.MarkPositions(point is { } marked ? [marked] : null);

    public void MarkWrite(int? index)
    {
        var block = IsOpen ? History.Store.Blocks[Block] : null;

        MarkPosition(block is not null && index is { } wanted && wanted >= 0 && wanted < block.WriteCount
                         ? History.Store.GetPoint(block.FirstWrite + wanted)
                         : null);
    }

    public void MarkWriter(string? label)
    {
        if (Chart is not { } chart)
        {
            return;
        }

        if (label is null || !IsOpen)
        {
            chart.MarkPositions(null);

            return;
        }

        var store = History.Store;

        var first = store.Blocks[Block].FirstWrite;

        var writes = store.GetWrites(Block);

        var points = new List<TracePoint>();

        for (var index = 0; index < writes.Count; index++)
        {
            if (History.GetWriterLabel(writes[index].CallNodeIndex) == label)
            {
                points.Add(store.GetPoint(first + index));
            }
        }

        chart.MarkPositions([.. points]);
    }

    public void ShowPageMarkers(int? offset)
    {
        if (offset is { } scroll)
        {
            ScrollToOffset = scroll;
        }

        ApplyMarkers();
    }

    public Windows.UI.Color GetCategoryColour(int callNodeIndex) => History.GetCategoryColour(callNodeIndex);

    partial void OnSelectedTabChanged(int value)
    {
        if (value != HistoryTab)
        {
            SelectedChangeSpan = null;
        }

        if (value is BatchTab or VectorsTab)
        {
            RefreshBatch();
        }

        RefreshPage();

        ApplyMarkers();
    }

    private void OnWatchesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Writes.Load();

    public void GoToChange(int offset, bool isForward)
    {
        if (!IsOpen)
        {
            return;
        }

        var store = History.Store;

        var writes = store.GetWrites(Block);

        var position = Playhead ?? OpenedAt;

        var index = isForward
            ? MemoryChangeNavigation.GetNextChange(writes, offset, position)
            : MemoryChangeNavigation.GetPreviousChange(writes, offset, position);

        GoToWrite(index);
    }

    public void SetPlayhead(TracePosition? position)
    {
        Playhead = position;

        Refresh();

        RefreshBatch();
    }

    private void Refresh()
    {
        if (!IsOpen)
        {
            return;
        }

        var store = History.Store;

        var position = Playhead ?? OpenedAt;

        var isLive = store.IsLive(Block, position);

        var count = isLive ? store.GetWriteCount(Block, position) : -1;

        if (count == WriteCount)
        {
            return;
        }

        WriteCount = count;

        if (!isLive)
        {
            HexLoader.Cancel();

            Data = null;

            WriterMarkers = null;

            Markers = null;

            Runs = [];

            Pointers = [];

            Links = null;

            References.SetPointers([]);

            WatchList.Refresh(null);

            Buffer.SetData(null);

            WorkfilePage.SetData(null);

            RefreshPage();

            return;
        }

        var block = Block;

        var history = History;

        HexLoader.Request(() => ReadHex(history, block, count, position));
    }

    private static HexImage? ReadHex(MemoryHistoryViewModel history, int block, int count, TracePosition position)
    {
        var store = history.Store;

        var image = store.GetImage(block, count - 1);

        return new HexImage(image.Data,
                            MemoryChangeRuns.GetRuns(store.GetWrites(block), count, image.Data.Length),
                            MemoryHistoryPointers.Find(store, history.BufferPool, block, image, count, position));
    }

    private List<HexLink> GetLinks(List<MemoryHistoryPointer> pointers)
    {
        var links = new List<HexLink>();

        foreach (var (offset, value, block, page) in pointers)
        {
            if (block >= 0)
            {
                var target = History.GetPointerTarget(value, Position) ?? $"0x{value:X16}";

                links.Add(new HexLink(offset,
                                      MemoryHistoryPointers.PointerSize,
                                      PointerPrefix + target,
                                      OpenMemoryCommand,
                                      () => RequestMemory(value)));
            }
            else if (page is { } reference && History.IsQueryDatabase(reference))
            {
                links.Add(new HexLink(offset,
                                      MemoryHistoryPointers.PointerSize,
                                      PointerPrefix + History.GetBufferPageLabel(reference),
                                      OpenPageCommand,
                                      () => Navigator?.OpenBufferPage(reference)));
            }
        }

        return links;
    }

    private void ApplyHex(HexImage? hex)
    {
        if (!IsOpen || hex is null)
        {
            return;
        }

        if (Data is not { } shown || !hex.Data.AsSpan().SequenceEqual(shown))
        {
            Data = hex.Data;

            WatchList.Refresh(Data);

            Buffer.SetData(Data);

            WorkfilePage.SetData(Data);

            RefreshPage();
        }

        if (WriterMarkers is null || !hex.Runs.SequenceEqual(Runs))
        {
            Runs = hex.Runs;

            WriterMarkers = new ObservableCollection<Marker>(GetChangeMarkers(hex.Runs));
        }

        if (Links is null || !hex.Pointers.SequenceEqual(Pointers))
        {
            Pointers = hex.Pointers;

            Links = GetLinks(hex.Pointers);

            References.SetPointers(hex.Pointers);
        }

        ApplyMarkers();
    }

    private void ApplyMarkers()
    {
        var markers = SelectedTab switch
        {
            BufferTab when Buffer.Markers is { } fields => fields,
            BatchTab when Batch.FieldMarkers is { } fields => fields,
            PageTab when WorkfilePage.IsAvailable => WorkfilePage.Markers,
            _ => WriterMarkers
        };

        if (!ReferenceEquals(Markers, markers))
        {
            Markers = markers;
        }
    }

    private void RefreshPage()
    {
        if (IsOpen && SelectedTab == PageTab)
        {
            WorkfilePage.Refresh();
        }
    }

    private void RefreshBatch()
    {
        if (IsOpen && SelectedTab is BatchTab or VectorsTab)
        {
            Batch.Refresh(Position);
        }
    }

    private IEnumerable<Marker> GetChangeMarkers(List<MemoryChangeRun> runs)
    {
        foreach (var run in runs)
        {
            var isPending = run.State == MemoryChangeState.Pending;

            var category = isPending ? null : (Windows.UI.Color?)GetCategoryColour(run.CallNodeIndex);

            yield return new Marker
            {
                Name = isPending ? NotYetChanged : History.GetWriterLabel(run.CallNodeIndex),
                StartPosition = run.Start,
                EndPosition = run.End,
                BackColour = category is { } colour ? ColourHelpers.Lighten(colour.ToColor()).ToWindowsColor() : Colors.WhiteSmoke,
                MapColour = category,
                ForeColour = Colors.Black
            };
        }
    }

    private sealed record HexImage(byte[] Data, List<MemoryChangeRun> Runs, List<MemoryHistoryPointer> Pointers);
}
