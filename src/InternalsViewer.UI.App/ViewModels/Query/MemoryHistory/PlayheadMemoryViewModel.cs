using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query.MemoryHistory.Enums;
using Microsoft.UI;

namespace InternalsViewer.UI.App.ViewModels.Query.MemoryHistory;

public sealed partial class PlayheadMemoryViewModel(MemoryHistoryViewModel history) : ObservableObject
{
    private const string NotYetChanged = "Not Yet Changed";

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
    private string _objectDetail = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    public event EventHandler<TracePoint>? PlayheadRequested;

    private MemoryHistoryViewModel History { get; } = history;

    private int Block { get; set; } = -1;

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

        var opened = History.Store.Blocks[block];

        var offset = address - opened.Address;

        Block = block;

        OpenedAt = position;

        WriteCount = -1;

        ObjectName = History.GetObjectName(block);

        Watches = History.GetWatches(block);

        ObjectDetail = offset == 0
            ? $"0x{opened.Address:X16}, {opened.Size} Bytes"
            : $"0x{opened.Address:X16} + 0x{offset:X}, {opened.Size} Bytes";

        IsOpen = true;

        Refresh();
    }

    public void Close()
    {
        Block = -1;

        Data = null;

        Markers = null;

        Watches = null;

        EmptyText = string.Empty;

        IsOpen = false;
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

        if (index >= 0)
        {
            PlayheadRequested?.Invoke(this, store.GetPoint(store.Blocks[Block].FirstWrite + index));
        }
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

            return;
        }

        var image = store.GetImage(Block, count - 1);

        if (Data is not { } shown || !image.Data.AsSpan().SequenceEqual(shown))
        {
            Data = image.Data;
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

            yield return new Marker
            {
                Name = isPending ? NotYetChanged : History.GetWriterLabel(run.CallNodeIndex),
                StartPosition = run.Start,
                EndPosition = run.End,
                BackColour = isPending ? Colors.WhiteSmoke : GetWriterColour(run.CallNodeIndex),
                ForeColour = Colors.Black
            };
        }
    }

    private Windows.UI.Color GetWriterColour(int callNodeIndex)
    {
        var colour = ColourHelpers.FromHex(History.GetWriter(callNodeIndex)?.CategoryColour) ?? Colors.Gray;

        return ColourHelpers.Lighten(colour.ToColor()).ToWindowsColor();
    }
}
