using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Services.Markers;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

/// <summary>
/// One column segment, its blob broken into the regions the structure table navigates
/// </summary>
public sealed partial class SegmentTabViewModel(ColumnstoreService columnstoreService,
                                                DatabaseSource database,
                                                SegmentSummary segment) : ObservableObject, IDisposable
{
    private ColumnstoreService ColumnstoreService { get; } = columnstoreService;

    private DatabaseSource Database { get; } = database;

    public SegmentSummary Segment { get; } = segment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBookmarks))]
    [NotifyPropertyChangedFor(nameof(HasRleArray))]
    [NotifyPropertyChangedFor(nameof(HasBitpackArray))]
    private SegmentBlob? _blob;

    public bool HasBookmarks => Blob is { BookmarkCount: > 0 };

    /// <summary>
    /// Whether the region exists at all, a store by value segment holding neither of the run length pair
    /// </summary>
    public bool HasRleArray => Blob?.Header.HasRleArray ?? false;

    public bool HasBitpackArray => Blob?.Header.HasBitpackArray ?? false;

    /// <summary>
    /// Whether the segment holds a paged value store in place of the run length and bit pack pair
    /// </summary>
    public bool HasValueStore => Blob?.ValueStore is not null;

    public ObservableCollection<ValuePageSummary> ValuePages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValuePageDescription))]
    private ValuePageSummary? _selectedValuePage;

    [ObservableProperty]
    private ValueList? _values;

    public string ValuePageDescription => SelectedValuePage is not { } page
        ? string.Empty
        : $"{page.ValueCount} values of {page.ValueSize} bytes, {page.CompressionDescription} bytes compressed";

    partial void OnSelectedValuePageChanged(ValuePageSummary? value)
    {
        Values = value is null ? null : new ValueList(value.Page);

        if (value is not null)
        {
            GoToOffset(value.Offset);
        }
    }

    /// <summary>
    /// Moves the window onto an offset without changing which region tab is on show
    /// </summary>
    private void GoToOffset(int offset)
    {
        var start = Math.Clamp(offset, 0, Math.Max(0, TotalLength - 1)) / 16 * 16;

        if (WindowOffset == start)
        {
            SetHexWindow(start);
        }
        else
        {
            WindowOffset = start;
        }
    }

    /// <summary>
    /// Resolves data ids to values, which the dictionary the segment reads has to be fetched for
    /// </summary>
    private SegmentValueDecoder? Decoder { get; set; }

    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>
    /// Whether the spinner is up, which a load only long enough to notice turns on
    /// </summary>
    [ObservableProperty]
    private bool _isSegmentLoading;

    private const int SpinnerDelayMs = 100;

    [ObservableProperty]
    private string _statusText = "Loading Segment...";

    [ObservableProperty]
    private byte[] _hexData = [];

    [ObservableProperty]
    private int _hexBaseAddress;

    [ObservableProperty]
    private bool _isHexViewVisible = true;

    public int TotalLength => Blob?.Data.Length ?? 0;

    /// <summary>
    /// Window the hex control asks for, which it sizes to the lines it can show and moves as it is scrolled
    /// </summary>
    [ObservableProperty]
    private int _windowOffset;

    [ObservableProperty]
    private int _windowLength;

    partial void OnWindowOffsetChanged(int value) => SetHexWindow(value);

    partial void OnWindowLengthChanged(int value) => SetHexWindow(WindowOffset);

    public ObservableCollection<SegmentElement> Elements { get; } = [];

    /// <summary>
    /// Every row of the segment, indexed rather than materialised so the grid only ever builds what it shows
    /// </summary>
    [ObservableProperty]
    private SegmentRowList? _rows;

    [ObservableProperty]
    private string _rowCountDescription = string.Empty;

    /// <summary>
    /// Whether the grids show the working behind a value, or only the value itself
    /// </summary>
    /// <remarks>
    /// The flag reaches a cell through the row it is bound to, so the rows are rebuilt to take a change. Both lists
    /// are cheap to rebuild, one being an index over the segment and the other a single packed unit.
    /// </remarks>
    [ObservableProperty]
    private bool _isDerivationVisible = true;

    partial void OnIsDerivationVisibleChanged(bool value)
    {
        if (Blob is { } blob)
        {
            BuildRows(blob);

            ValuePages.Clear();

            if (blob.ValueStore is { } store)
            {
                for (var i = 0; i < store.Pages.Length; i++)
                {
                    ValuePages.Add(new ValuePageSummary { Index = i, Page = store.Pages[i] });
                }
            }

            OnPropertyChanged(nameof(HasValueStore));
        }

        BitpackUnit = GetBitpackUnit(SelectedMarker);
    }

    /// <summary>
    /// Region the window sits on, set by picking a tab and reported back when a scroll leaves the region
    /// </summary>
    [ObservableProperty]
    private SegmentRegion _region = SegmentRegion.Header;

    partial void OnRegionChanged(SegmentRegion value)
    {
        if (_isFollowingWindow)
        {
            return;
        }

        GoToRegion(value);
    }

    /// <summary>
    /// Whether scrolling out of a region moves on to the tab for the region scrolled into
    /// </summary>
    [ObservableProperty]
    private bool _isAutoRegion = true;

    /// <summary>
    /// Set while the region is being brought into line with the window, so it does not move the window in turn
    /// </summary>
    private bool _isFollowingWindow;

    /// <summary>
    /// Set while the window is being moved to a region, the region being the cause rather than something to follow
    /// </summary>
    private bool _isJumpingToRegion;

    /// <summary>
    /// Replaced rather than mutated, the marker controls rebuilding only when the property itself changes
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Marker> _markers = [];

    /// <summary>
    /// Whether the markers are behind the window, which dims them until the rebuild catches up
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MarkerOpacity))]
    private bool _areMarkersStale;

    public double MarkerOpacity => AreMarkersStale ? 0.35 : 1.0;

    private const int MarkerDelayMs = 120;

    private CancellationTokenSource? _markerDebounce;

    [ObservableProperty]
    private Marker? _selectedMarker;

    /// <summary>
    /// Unit the bit ruler breaks apart, found from the marker selected in the bit pack region
    /// </summary>
    [ObservableProperty]
    private BitpackUnitDetail? _bitpackUnit;

    partial void OnSelectedMarkerChanged(Marker? value) => BitpackUnit = GetBitpackUnit(value);

    /// <summary>
    /// Hands the grid an indexed view over the segment, the rows themselves being worked out on demand
    /// </summary>
    private void BuildRows(SegmentBlob blob)
    {
        var stream = new SegmentDataIdStream(blob);

        var context = new SegmentRowContext(blob, stream, DeriveValue, IsDerivationVisible);

        Rows = new SegmentRowList(context, stream.RowCount);

        RowCountDescription = stream.RowCount == 1 ? "1 row" : $"{stream.RowCount} rows";
    }

    private ValueDerivation? DeriveValue(long dataId)
        => Decoder is { } decoder ? SegmentValueDerivation.Build(Segment.Segment, decoder, dataId) : null;

    private BitpackUnitDetail? GetBitpackUnit(Marker? marker)
    {
        if (Blob is not { } blob
            || blob.IsStoreByValue
            || Region != SegmentRegion.BitpackArray
            || marker is not { StartPosition: >= 0 })
        {
            return null;
        }

        var offset = HexBaseAddress + marker.StartPosition - blob.BitpackArrayOffset;

        var index = offset / BitpackArray.UnitBytes;

        if (index < 0 || index >= blob.BitpackUnitCount)
        {
            return null;
        }

        return BitpackUnitDetail.Build(blob, index, DeriveValue, IsDerivationVisible);
    }

    /// <summary>
    /// Moves the window to the region's first line, or rebuilds in place if it is already there
    /// </summary>
    private void GoToRegion(SegmentRegion region)
    {
        _isJumpingToRegion = true;

        try
        {
            var offset = GetRegionOffset(region);

            if (WindowOffset == offset)
            {
                SetHexWindow(offset);

                return;
            }

            WindowOffset = offset;
        }
        finally
        {
            _isJumpingToRegion = false;
        }
    }

    private int GetRegionOffset(SegmentRegion region)
        => Blob is not { } blob ? 0 : SegmentRegions.GetOffset(blob, region) / 16 * 16;

    public void Dispose()
    {
        _markerDebounce?.Cancel();
        _markerDebounce?.Dispose();
        _markerDebounce = null;
    }

    public async Task Load(CancellationToken cancellationToken)
    {
        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowSpinnerAfterDelay(spinnerDelay.Token);

        try
        {
            var blob = await Task.Run(
                () => ColumnstoreService.GetSegmentBlob(Database, Segment.Segment, cancellationToken, isMarkEnabled: true),
                cancellationToken);

            var decoder = await Task.Run(
                () => ColumnstoreService.GetSegmentDecoder(Database, Segment.Segment, cancellationToken),
                cancellationToken);

            await spinnerDelay.CancelAsync();

            Decoder = decoder;

            Blob = blob;

            Elements.Clear();

            foreach (var element in SegmentElementBuilder.Build(blob))
            {
                Elements.Add(element);
            }

            OnPropertyChanged(nameof(TotalLength));

            BuildRows(blob);

            GoToRegion(Region);

            StatusText = $"{blob.Data.Length} bytes";

            IsLoaded = true;
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
        finally
        {
            await spinnerDelay.CancelAsync();

            IsSegmentLoading = false;
        }
    }

    /// <summary>
    /// Holds the spinner back so a segment that reads straight away does not flash one up
    /// </summary>
    private async Task ShowSpinnerAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(SpinnerDelayMs, token);

            if (!token.IsCancellationRequested)
            {
                IsSegmentLoading = true;
            }
        }
        catch (TaskCanceledException)
        {
            // The segment read finished inside the delay, so no spinner is wanted
        }
    }

    /// <summary>
    /// Moves the window so it starts on the line the offset falls in, the markers following once scrolling settles
    /// </summary>
    /// <remarks>
    /// Only the hex slice is done here. Marker rebuilding walks the region and the marker tree rebuilds its nodes
    /// from what comes back, which is far too much to repeat on every wheel notch, so it is left to the debounce.
    /// </remarks>
    private void SetHexWindow(int offset)
    {
        if (Blob is not { } blob || blob.Data.Length == 0 || WindowLength <= 0)
        {
            HexData = [];

            Markers = [];

            return;
        }

        var start = Math.Clamp(offset, 0, Math.Max(0, blob.Data.Length - 1)) / 16 * 16;

        var length = Math.Min(WindowLength, blob.Data.Length - start);

        HexBaseAddress = start;

        HexData = blob.Data.Slice(start, length).ToArray();

        SelectedMarker = null;

        ClearMarkers();

        ScheduleMarkers();

        FollowWindow(blob, start);
    }

    /// <summary>
    /// Drops the markers the moment the window moves, their positions being relative to the window they were built for
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

    /// <summary>
    /// Waits for the window to settle, so a run of scroll steps costs one marker build rather than one each
    /// </summary>
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

        if (token.IsCancellationRequested)
        {
            return;
        }

        BuildMarkers();
    }

    private void BuildMarkers()
    {
        if (Blob is not { } blob || HexData.Length == 0)
        {
            return;
        }

        Markers = new ObservableCollection<Marker>(
            SegmentRegionMarkerBuilder.Build(blob, Region, HexBaseAddress, HexData.Length));

        AreMarkersStale = false;
    }

    /// <summary>
    /// Moves to the item the target names, showing its region and selecting the marker over it
    /// </summary>
    /// <remarks>
    /// The markers are built rather than left to the debounce, there being nothing to select until they exist.
    /// </remarks>
    public void GoToTarget(SegmentNavigationTarget target)
    {
        if (Blob is not { } blob)
        {
            return;
        }

        _isFollowingWindow = true;

        Region = target.Region;

        _isFollowingWindow = false;

        var start = Math.Clamp(target.Offset, 0, Math.Max(0, blob.Data.Length - 1)) / 16 * 16;

        if (WindowOffset == start)
        {
            SetHexWindow(start);
        }
        else
        {
            WindowOffset = start;
        }

        // Moving the window scheduled a rebuild, which would replace the collection and drop the selection with it
        _markerDebounce?.Cancel();

        BuildMarkers();

        SelectedMarker = Markers.FirstOrDefault(m => m.StartPosition == target.Offset - HexBaseAddress);
    }

    /// <summary>
    /// Brings the region into line with the window, so a scroll past a boundary moves on to the tab it landed in
    /// </summary>
    /// <remarks>
    /// A jump is skipped because the region is what moved the window, and following it back would only fight the
    /// line alignment - a region starting part way into a line resolves to the region before it.
    /// </remarks>
    private void FollowWindow(SegmentBlob blob, int start)
    {
        if (!IsAutoRegion || _isJumpingToRegion)
        {
            return;
        }

        var region = SegmentRegions.GetRegion(blob, start);

        if (region == Region)
        {
            return;
        }

        _isFollowingWindow = true;

        Region = region;

        _isFollowingWindow = false;

        SetHexWindow(start);
    }
}
