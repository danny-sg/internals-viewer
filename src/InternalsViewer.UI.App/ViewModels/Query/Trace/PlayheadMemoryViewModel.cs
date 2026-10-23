using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.ViewModels.Query.MemoryHistory;
using InternalsViewer.UI.App.ViewModels.Query.Trace.Enums;
using Microsoft.UI;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class PlayheadMemoryViewModel : ObservableObject
{
    private const string NotYetChanged = "Not Yet Changed";

    private const int HistoryTab = 1;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private byte[]? _data;

    [ObservableProperty]
    private ObservableCollection<Marker>? _markers;

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

    public PlayheadMemoryViewModel(MemoryHistoryViewModel history)
    {
        History = history;

        Writes = new MemoryWritesViewModel(this);
    }

    public event EventHandler<TracePoint>? PlayheadRequested;

    public MemoryHistoryViewModel History { get; }

    public MemoryDetailsViewModel Details { get; } = new();

    public MemoryWritesViewModel Writes { get; }

    public MemoryWatchesViewModel WatchList { get; } = new();

    public int Block { get; private set; } = -1;

    public IPlayheadChart? Chart { get; set; }

    private TracePosition OpenedAt { get; set; } = TracePosition.Unknown;

    private TracePosition? Playhead { get; set; }

    private int WriteCount { get; set; } = -1;

    private List<MemoryChangeRun> Runs { get; set; } = [];

    public void Open(int block, ulong address, TracePosition position)
    {
        if (block < 0 || block >= History.Store.Blocks.Count)
        {
            return;
        }

        Block = block;

        OpenedAt = position;

        WriteCount = -1;

        ObjectName = History.GetObjectName(block);

        Watches = History.GetWatches(block);

        Watches.CollectionChanged += OnWatchesChanged;

        IsOpen = true;

        SelectedChangeSpan = null;

        Refresh();

        Details.Load(this);

        Writes.Load();

        WatchList.Attach(Watches);

        ShowAddress(address);
    }

    public void ShowAddress(ulong address)
    {
        if (!IsOpen)
        {
            return;
        }

        var block = History.Store.Blocks[Block];

        var offset = address - block.Address;

        SelectedChangeSpan = offset > 0 && offset < (ulong)block.Size
            ? new LogRecordAnnotation { Offset = (int)offset, Length = 1, Description = $"0x{address:X16}" }
            : null;
    }

    public void Close()
    {
        WatchList.Attach(null);

        Writes.Clear();

        Details.Clear();

        Block = -1;

        Data = null;

        Markers = null;

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

    public void MarkPosition(TracePoint? point) => Chart?.MarkPosition(point);

    public void MarkWrite(int? index)
    {
        var block = IsOpen ? History.Store.Blocks[Block] : null;

        MarkPosition(block is not null && index is { } wanted && wanted >= 0 && wanted < block.WriteCount
                         ? History.Store.GetPoint(block.FirstWrite + wanted)
                         : null);
    }

    public void HighlightWriter(string? label)
    {
        if (Chart is not { } chart)
        {
            return;
        }

        if (label is null || !IsOpen)
        {
            chart.HighlightCalls(null);

            return;
        }

        var writers = new HashSet<CallStackNode>(ReferenceEqualityComparer.Instance);

        foreach (var write in History.Store.GetWrites(Block))
        {
            if (History.GetWriter(write.CallNodeIndex) is { } node && History.GetWriterLabel(write.CallNodeIndex) == label)
            {
                writers.Add(node);
            }
        }

        chart.HighlightCalls(writers);
    }

    public Windows.UI.Color GetCategoryColour(int callNodeIndex)
        => ColourHelpers.FromHex(History.GetWriter(callNodeIndex)?.CategoryColour) ?? Colors.Gray;

    partial void OnSelectedTabChanged(int value)
    {
        if (value != HistoryTab)
        {
            SelectedChangeSpan = null;
        }
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
            Data = null;

            Markers = null;

            Runs = [];

            WatchList.Refresh(null);

            return;
        }

        var image = store.GetImage(Block, count - 1);

        if (Data is not { } shown || !image.Data.AsSpan().SequenceEqual(shown))
        {
            Data = image.Data;

            WatchList.Refresh(Data);
        }

        var runs = MemoryChangeRuns.GetRuns(store.GetWrites(Block), count, image.Data.Length);

        if (Markers is null || !runs.SequenceEqual(Runs))
        {
            Runs = runs;

            Markers = new ObservableCollection<Marker>(GetChangeMarkers(runs));
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
}
