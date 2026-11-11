using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Hash.Enums;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Pages;
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

    private const int BufferTab = 0;

    private const int BatchTab = 1;

    private const int VectorsTab = 2;

    private const int PageTab = 3;

    private const int BindingsTab = 4;

    private const int IteratorTab = 5;

    private const int DetailsTab = 6;

    private const int WritesTab = 7;

    private const int ReadsTab = 8;

    private const int LoadInterval = 50;

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
    private int _selectedTab = DetailsTab;

    [ObservableProperty]
    private LogRecordAnnotation? _selectedChangeSpan;

    [ObservableProperty]
    private int? _scrollToOffset;

    [ObservableProperty]
    private bool _isHashTableVisible;

    [ObservableProperty]
    private string _hashTableText = string.Empty;

    [ObservableProperty]
    private string _hashTablePartText = string.Empty;

    public PlayheadMemoryViewModel(MemoryHistoryViewModel history)
    {
        History = history;

        Writes = new MemoryWritesViewModel(this);

        Reads = new MemoryReadsViewModel(this);

        References = new MemoryReferencesViewModel(this);

        WorkfilePage = new MemoryPageViewModel(ShowMarkers);

        HexLoader = new PlayheadLoader<HexImage?>(TimeSpan.FromMilliseconds(LoadInterval), ApplyHex);
    }

    public event EventHandler<TracePoint>? PlayheadRequested;

    public event EventHandler<MemoryRequest>? MemoryRequested;

    public MemoryHistoryViewModel History { get; }

    public MemoryDetailsViewModel Details { get; } = new();

    public MemoryWritesViewModel Writes { get; }

    public MemoryReadsViewModel Reads { get; }

    public MemoryWatchesViewModel WatchList { get; } = new();

    public MemoryReferencesViewModel References { get; }

    public MemoryBufferViewModel Buffer { get; } = new();

    public MemoryBatchViewModel Batch { get; } = new();

    public MemoryPageViewModel WorkfilePage { get; }

    public MemoryBindingsViewModel Bindings { get; } = new();

    public MemoryIteratorViewModel Iterator { get; } = new();

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

    private ulong HashTableGroup { get; set; }

    private TraceHashPart? HashTablePart { get; set; }

    private RowRecordLayout? HashTableLayout { get; set; }

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

        Reads.Load();

        WatchList.Attach(Watches);

        Buffer.Load(this);

        Batch.Load(this);

        FindHashTable();

        WorkfilePage.Load(History, block, HashTableLayout);

        Bindings.Load(this);

        Iterator.Load(this);

        ApplyMarkers();

        if (!IsTabAvailable(SelectedTab) || SelectedTab == DetailsTab)
        {
            SelectedTab = GetFirstTab();
        }

        RefreshBatch();

        RefreshBindings();

        RefreshIterator();

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

        Bindings.Clear();

        Iterator.Clear();

        HashTableGroup = 0;

        HashTablePart = null;

        IsHashTableVisible = false;

        HashTableText = string.Empty;

        HashTablePartText = string.Empty;

        Writes.Clear();

        Reads.Clear();

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

    public object GetPointerCell(ulong pointer)
    {
        var text = $"0x{pointer:X16}";

        if (pointer == 0)
        {
            return text;
        }

        if (History.GetBlock(pointer, Position) < 0)
        {
            return GetBufferPageCell(text, pointer, Position) ?? text;
        }

        return new ActionLinks([new ActionLink(text, () => RequestMemory(pointer))])
        {
            Suffix = History.GetPointerTarget(pointer, Position)
        };
    }

    public void OpenHashTable()
    {
        if (HashTableGroup != 0)
        {
            Navigator?.OpenHashTable(HashTableGroup, HashTablePart);
        }
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

    public void ShowWrites(string? function, ulong? address)
    {
        Writes.SetFilters(function, address);

        SelectedTab = WritesTab;
    }

    public void ShowReads(string? function, ulong? address)
    {
        Reads.SetFilters(function, address);

        SelectedTab = ReadsTab;
    }

    public void ClearSelection() => SelectedChangeSpan = null;

    public void GoToRead(int index)
    {
        if (GetReadIndex(index) is { } read)
        {
            RequestPlayhead(History.Store.GetReadPoint(read));
        }
    }

    public void SelectRead(int index)
    {
        if (GetReadIndex(index) is not { } found)
        {
            return;
        }

        var read = History.Store.GetRead(found);

        GoToRead(index);

        SelectedChangeSpan = new LogRecordAnnotation
        {
            Offset = read.Offset,
            Length = read.Length,
            Description = History.GetWriterLabel(read.CallNodeIndex),
            OutlineColour = GetReadColour(read.CallNodeIndex)
        };
    }

    public void MarkRead(int? index)
        => MarkPosition(index is { } wanted && GetReadIndex(wanted) is { } found ? History.Store.GetReadPoint(found) : null);

    public Windows.UI.Color GetReadColour(int callNodeIndex)
    {
        var colour = GetCategoryColour(callNodeIndex);

        return Windows.UI.Color.FromArgb(colour.A,
                                         (byte)(colour.R + (255 - colour.R) / 2),
                                         (byte)(colour.G + (255 - colour.G) / 2),
                                         (byte)(colour.B + (255 - colour.B) / 2));
    }

    public void ShowWritesAt(int offset)
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

        ShowWrites(function, History.Store.Blocks[Block].Address + (ulong)offset);
    }

    public void MarkPosition(TracePoint? point) => Chart?.MarkPositions(point is { } marked ? [marked] : null);

    public void MarkWrite(int? index)
    {
        var block = IsOpen ? History.Store.Blocks[Block] : null;

        MarkPosition(block is not null && index is { } wanted && wanted >= 0 && wanted < block.WriteCount
                         ? History.Store.GetPoint(block.FirstWrite + wanted)
                         : null);
    }

    public void MarkReader(string? label)
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

        var (first, count) = store.GetReadRange(Block);

        var points = new List<TracePoint>();

        for (var index = first; index < first + count; index++)
        {
            if (History.GetWriterLabel(store.GetRead(index).CallNodeIndex) == label)
            {
                points.Add(store.GetReadPoint(index));
            }
        }

        chart.MarkPositions([.. points]);
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

    public void ShowMarkers(int? offset)
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
        if (value is not (WritesTab or ReadsTab))
        {
            SelectedChangeSpan = null;
        }

        if (value is BatchTab or VectorsTab)
        {
            RefreshBatch();
        }

        RefreshPage();

        RefreshBindings();

        RefreshIterator();

        ApplyMarkers();
    }

    private void OnWatchesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Writes.Load();

    private void FindHashTable()
    {
        var (group, found, table, position) = History.CallStore is { } calls ? FindHashTablePart(calls) : (0, null, null, default);

        (HashTableGroup, HashTablePart) = (group, found);

        HashTableLayout = found is { Type: TraceHashPartType.WorkfilePage } && table is not null
            ? GetHashTableLayout(table, position)
            : null;

        IsHashTableVisible = HashTableGroup != 0;

        HashTableText = IsHashTableVisible ? $"0x{HashTableGroup:X16}" : string.Empty;

        HashTablePartText = HashTablePart is { } part ? HashTablePartLabel.GetText(part) : string.Empty;
    }

    private RowRecordLayout? GetHashTableLayout(TraceHashTable table, TracePosition position)
    {
        if (table.Columns.Count == 0)
        {
            return null;
        }

        var slot = TraceHashTableReader.GetIteratorGroups(History.Store, History.AddressMap, table.Iterator, position)
                                       .Find(g => g.Group == table.Group)
                                       .Slot;

        return History.GetWorkfileLayout(table.Iterator,
                                         [.. table.Columns.Select(c => c.Binding)],
                                         slot / TraceHashTableLayout.GroupsPerSide == 1);
    }

    private (ulong Group, TraceHashPart? Part, TraceHashTable? Table, TracePosition Position) FindHashTablePart(TraceCallStore calls)
    {
        var block = History.Store.Blocks[Block];

        foreach (var group in TraceHashTableReader.GetGroups(calls))
        {
            var accesses = TraceHashTableReader.GetAccesses(calls, group);

            List<TracePosition> positions = accesses.Count > 0 ? [OpenedAt, accesses[^1].Position] : [OpenedAt];

            foreach (var position in positions)
            {
                if (TraceHashTableReader.Read(History.Store, group, position) is { } table
                    && table.FindPart(block.Address, block.Size) is { } part)
                {
                    return (group, part, table, position);
                }
            }
        }

        return (0, null, null, default);
    }

    private int? GetReadIndex(int index)
    {
        if (!IsOpen)
        {
            return null;
        }

        var (first, count) = History.Store.GetReadRange(Block);

        return index >= 0 && index < count ? first + index : null;
    }

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

        RefreshBindings();

        RefreshIterator();
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
                            MemoryHistoryPointerReader.Find(store, history.BufferPool, block, image, count, position));
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
                                      MemoryHistoryPointerReader.PointerSize,
                                      PointerPrefix + target,
                                      OpenMemoryCommand,
                                      () => RequestMemory(value)));
            }
            else if (page is { } reference && History.IsQueryDatabase(reference))
            {
                links.Add(new HexLink(offset,
                                      MemoryHistoryPointerReader.PointerSize,
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

            Iterator.SetData(Data);

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
            BindingsTab when Bindings.Markers is { } fields => fields,
            IteratorTab when Iterator.Markers is { } fields => fields,
            _ => WriterMarkers
        };

        if (!ReferenceEquals(Markers, markers))
        {
            Markers = markers;
        }
    }

    private bool IsTabAvailable(int tab) => tab switch
    {
        BufferTab => Buffer.IsAvailable,
        BatchTab => Batch.IsAvailable,
        VectorsTab => Batch.IsVectorsAvailable,
        PageTab => WorkfilePage.IsAvailable,
        BindingsTab => Bindings.IsAvailable,
        IteratorTab => Iterator.IsAvailable,
        _ => true
    };

    private int GetFirstTab()
    {
        for (var tab = BufferTab; tab < DetailsTab; tab++)
        {
            if (IsTabAvailable(tab))
            {
                return tab;
            }
        }

        return DetailsTab;
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

    private void RefreshBindings()
    {
        if (IsOpen && SelectedTab == BindingsTab)
        {
            Bindings.Refresh(Position);

            ApplyMarkers();
        }
    }

    private void RefreshIterator()
    {
        if (IsOpen && SelectedTab == IteratorTab)
        {
            Iterator.Refresh();

            ApplyMarkers();
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
