using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

/// <summary>
/// A window over a blob for the hex view, with the markers for whatever the window is showing
/// </summary>
/// <remarks>
/// The window holds only the lines on screen, so a blob of any size costs the same to show. Markers are rebuilt
/// after scrolling settles rather than on every step, a rebuild walking the region and the marker tree rebuilding
/// its nodes from what comes back.
/// </remarks>
public sealed partial class BlobHexViewModel : ObservableObject, IDisposable
{
    public const int BytesPerLine = 16;

    private const int MarkerDelayMs = 120;

    /// <summary>
    /// Bytes the window holds until the control has laid out and can say how many lines it fits
    /// </summary>
    private const int DefaultWindowLength = BytesPerLine * 32;

    private CancellationTokenSource? _markerDebounce;

    private ReadOnlyMemory<byte> _data;

    [ObservableProperty]
    private byte[] _hexData = [];

    [ObservableProperty]
    private int _hexBaseAddress;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private int _windowOffset;

    [ObservableProperty]
    private int _windowLength;

    /// <summary>
    /// Replaced rather than mutated, the marker controls rebuilding only when the property itself changes
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Marker> _markers = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarkerOpacity))]
    private bool _areMarkersStale;

    [ObservableProperty]
    private Marker? _selectedMarker;

    /// <summary>
    /// Raised once the window has moved, for an owner tracking which part of the blob is on show
    /// </summary>
    public event EventHandler<int>? WindowMoved;

    /// <summary>
    /// Builds the markers for a window, given where it starts and how long it is
    /// </summary>
    public Func<int, int, List<Marker>>? MarkerFactory { get; set; }

    public int TotalLength => _data.Length;

    public double MarkerOpacity => AreMarkersStale ? 0.35 : 1.0;

    public void SetData(ReadOnlyMemory<byte> data)
    {
        _data = data;

        OnPropertyChanged(nameof(TotalLength));

        // Data can arrive before the control has measured itself, and a window of nothing builds no markers
        if (WindowLength <= 0)
        {
            WindowLength = DefaultWindowLength;

            return;
        }

        SetWindow(WindowOffset);
    }

    /// <summary>
    /// Moves the window to the line the offset falls on, rebuilding the markers straight away rather than waiting
    /// </summary>
    public void GoToOffset(int offset)
    {
        if (!IsOffsetVisible(offset))
        {
            var start = Align(offset);

            if (WindowOffset == start)
            {
                SetWindow(start);
            }
            else
            {
                WindowOffset = start;
            }
        }

        // Moving the window scheduled a rebuild, which would replace the collection and drop any selection with it
        _markerDebounce?.Cancel();

        BuildMarkers();
    }

    /// <summary>
    /// Whether the offset is one the window is already showing, which is what makes moving to it unnecessary
    /// </summary>
    public bool IsOffsetVisible(int offset)
        => HexData.Length > 0 && offset >= HexBaseAddress && offset < HexBaseAddress + HexData.Length;

    public void BuildMarkers()
    {
        if (_data.Length == 0 || HexData.Length == 0 || MarkerFactory is not { } factory)
        {
            return;
        }

        Markers = new ObservableCollection<Marker>(factory(HexBaseAddress, HexData.Length));

        AreMarkersStale = false;
    }

    public void Dispose()
    {
        _markerDebounce?.Cancel();
        _markerDebounce?.Dispose();
        _markerDebounce = null;
    }

    partial void OnWindowOffsetChanged(int value) => SetWindow(value);

    partial void OnWindowLengthChanged(int value) => SetWindow(WindowOffset);

    private int Align(int offset)
        => Math.Clamp(offset, 0, Math.Max(0, _data.Length - 1)) / BytesPerLine * BytesPerLine;

    /// <summary>
    /// Slices the blob for the hex view, the markers following once scrolling settles
    /// </summary>
    private void SetWindow(int offset)
    {
        if (_data.Length == 0 || WindowLength <= 0)
        {
            HexData = [];

            Markers = [];

            return;
        }

        var start = Align(offset);

        var length = Math.Min(WindowLength, _data.Length - start);

        HexBaseAddress = start;

        HexData = _data.Slice(start, length).ToArray();

        SelectedMarker = null;

        ClearMarkers();

        ScheduleMarkers();

        WindowMoved?.Invoke(this, start);
    }

    /// <summary>
    /// Drops the markers as the window moves, their positions being relative to the window they were built for
    /// </summary>
    private void ClearMarkers()
    {
        AreMarkersStale = true;

        if (Markers.Count > 0)
        {
            Markers = [];
        }
    }

    private void ScheduleMarkers()
    {
        _markerDebounce?.Cancel();
        _markerDebounce?.Dispose();

        _markerDebounce = new CancellationTokenSource();

        _ = BuildMarkersAfterDelay(_markerDebounce.Token);
    }

    private async Task BuildMarkersAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(MarkerDelayMs, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (!token.IsCancellationRequested)
        {
            BuildMarkers();
        }
    }
}
