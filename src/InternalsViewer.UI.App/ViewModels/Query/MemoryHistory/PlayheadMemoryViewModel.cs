using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.Models;
using Microsoft.UI;

namespace InternalsViewer.UI.App.ViewModels.Query.MemoryHistory;

public sealed partial class PlayheadMemoryViewModel(MemoryHistoryViewModel history) : ObservableObject
{
    private const string NotAllocated = "Not Allocated At This Position";

    private const string Changed = "Changed";

    private static readonly Windows.UI.Color ChangedColour = Windows.UI.Color.FromArgb(255, 255, 246, 196);

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private byte[]? _data;

    [ObservableProperty]
    private ObservableCollection<Marker>? _markers;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private string _objectDetail = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    private MemoryHistoryViewModel History { get; } = history;

    private int Block { get; set; } = -1;

    private TracePosition OpenedAt { get; set; } = TracePosition.Unknown;

    private TracePosition? Playhead { get; set; }

    private int WriteCount { get; set; } = -1;

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

        EmptyText = string.Empty;

        IsOpen = false;
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

        EmptyText = isLive ? string.Empty : NotAllocated;

        if (count == WriteCount)
        {
            return;
        }

        WriteCount = count;

        var image = isLive ? store.GetImage(Block, count - 1) : null;

        Data = image?.Data;

        Markers = image is null ? null : new ObservableCollection<Marker>(GetChangedMarkers(image.Changed));
    }

    private static IEnumerable<Marker> GetChangedMarkers(bool[] changed)
    {
        var start = -1;

        for (var offset = 0; offset <= changed.Length; offset++)
        {
            var isChanged = offset < changed.Length && changed[offset];

            if (isChanged && start < 0)
            {
                start = offset;
            }
            else if (!isChanged && start >= 0)
            {
                yield return new Marker
                {
                    Name = Changed,
                    StartPosition = start,
                    EndPosition = offset - 1,
                    BackColour = ChangedColour,
                    ForeColour = Colors.Black
                };

                start = -1;
            }
        }
    }
}
