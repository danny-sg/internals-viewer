using System;
using System.Collections.Generic;
using System.Data;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Services.Markers;
using InternalsViewer.UI.App.Models.Columnstore.Segment;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

/// <summary>
/// One column segment, its blob broken into the regions the structure table navigates
/// </summary>
public sealed partial class SegmentTabViewModel(ColumnstoreService columnstoreService,
                                                DatabaseSource database,
                                                SegmentSummary segment,
                                                Action<SegmentSummary>? openDictionary = null) : ObservableObject, IDisposable
{
    /// <summary>
    /// Opens the dictionary the segment reads, which only the dock above this tab knows how to do
    /// </summary>
    private Action<SegmentSummary>? OpenDictionaryAction { get; } = openDictionary;

    public bool HasDictionary => Segment.HasDictionary;

    /// <summary>
    /// Whether the dictionary is the column's own or one built for this row group, as a chip beside the button
    /// </summary>
    public IReadOnlyList<SegmentBadge> DictionaryBadges => Segment.Dictionary is not { } dictionary
        ? []
        : [SegmentBadge.Create(Segment.DictionaryScope, ColumnstoreLayout.GetDictionaryColour(dictionary.Type))];

    public void OpenDictionary() => OpenDictionaryAction?.Invoke(Segment);

    public string GetCsIndexCommand(int printMode)
        => CsIndexCommand.Build(Segment, Database.DatabaseId, Segment.Segment.Key.HobtId, printMode);

    private ColumnstoreService ColumnstoreService { get; } = columnstoreService;

    private DatabaseSource Database { get; } = database;

    public SegmentSummary Segment { get; } = segment;

    /// <summary>
    /// The type the column was declared as, which the header shows beside its name
    /// </summary>
    /// <remarks>
    /// Surfaced a field at a time because a data type is drawn from four of them, and x:Bind cannot reach through a
    /// structure that may not be there.
    /// </remarks>
    public SqlDbType? DataType => Segment.Structure?.DataType;

    public int Precision => Segment.Structure?.Precision ?? 0;

    public int Scale => Segment.Structure?.Scale ?? 0;

    public int DataLength => Segment.Structure?.DataLength ?? 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBookmarks))]
    [NotifyPropertyChangedFor(nameof(Bookmarks))]
    [NotifyPropertyChangedFor(nameof(BitpackUnits))]
    [NotifyPropertyChangedFor(nameof(HasRleArray))]
    [NotifyPropertyChangedFor(nameof(RleRuns))]
    [NotifyPropertyChangedFor(nameof(RleValueLabel))]
    [NotifyPropertyChangedFor(nameof(HexAreas))]
    [NotifyPropertyChangedFor(nameof(RleIndexLabel))]
    [NotifyPropertyChangedFor(nameof(HasBitpackArray))]
    [NotifyPropertyChangedFor(nameof(HasVariableLengthData))]
    [NotifyPropertyChangedFor(nameof(StorageBadges))]
    private SegmentBlob? _blob;

    partial void OnBlobChanged(SegmentBlob? value)
    {
        _rleRuns = null;
        _bookmarks = null;
        _bitpackUnits = null;
        _hexAreas = null;
        _dataIdStream = null;
    }

    private IReadOnlyList<RleRunDetail>? _rleRuns;

    private IReadOnlyList<BookmarkDetail>? _bookmarks;

    private BitpackUnitList? _bitpackUnits;

    private IReadOnlyList<HexArea>? _hexAreas;

    private SegmentDataIdStream? _dataIdStream;

    public bool HasBookmarks => Blob is { Header.BookmarkCount: > 0 };

    /// <summary>
    /// Every bookmark of the segment, listed rather than marked, the array running to thousands of entries
    /// </summary>
    public IReadOnlyList<BookmarkDetail> Bookmarks => _bookmarks ??= BuildBookmarks();

    private IReadOnlyList<BookmarkDetail> BuildBookmarks()
    {
        if (Blob is not { } blob)
        {
            return [];
        }

        var details = new List<BookmarkDetail>(blob.Bookmarks.Length);

        for (var i = 0; i < blob.Bookmarks.Length; i++)
        {
            var bookmark = blob.Bookmarks[i];

            var rleEntryIndex = bookmark.GetRleEntryIndex(blob.Header.RleEntryBytes);

            details.Add(new BookmarkDetail(i,
                                           bookmark.Position,
                                           rleEntryIndex,
                                           bookmark.EndRow,
                                           blob.Header.BookmarkArrayOffset + (i * SegmentBlob.EntrySize),
                                           blob.Header.RleEntryBytes,
                                           blob.Header.RleArrayOffset + (rleEntryIndex * blob.Header.RleEntryBytes)));
        }

        return details;
    }

    public void SelectBookmark(BookmarkDetail? bookmark)
    {
        if (bookmark is not null)
        {
            GoToTarget(new SegmentNavigationTarget(SegmentRegion.Bookmarks, bookmark.Offset));
        }
    }

    /// <summary>
    /// Units of the bit pack array, listed lazily, a segment holding hundreds of thousands of them
    /// </summary>
    public BitpackUnitList? BitpackUnits => Blob is { Header.HasBitpackArray: true } blob
        ? _bitpackUnits ??= new BitpackUnitList(blob)
        : null;

    public void SelectBitpackUnit(BitpackUnitRow? row)
    {
        if (Blob is not { } blob || row is null)
        {
            return;
        }

        BitpackUnit = BitpackUnitDetail.Build(blob, row.Unit, DeriveDataIdValue, IsDerivationVisible);

        GoToTarget(new SegmentNavigationTarget(SegmentRegion.BitpackArray, row.Offset));
    }


    /// <summary>
    /// How the column was compressed, which the metadata decides before the blob is ever read
    /// </summary>
    public IReadOnlyList<SegmentBadge> EncodingBadges =>
    [
        SegmentBadge.Create(Segment.EncodingDescription, ColumnstoreLayout.GetEncodingColour(Segment.Encoding))
    ];

    /// <summary>
    /// Which store the rows came out of, which the blob decides rather than the metadata
    /// </summary>
    public IReadOnlyList<SegmentBadge> StorageBadges
    {
        get
        {
            var storage = SegmentStorageExtensions.Classify(Blob?.Header);

            return storage == SegmentStorage.Unknown
                ? []
                : [SegmentBadge.Create(storage.Describe(), ColumnstoreLayout.GetStorageColour(storage))];
        }
    }

    /// <summary>
    /// Whether the region exists at all, a store by value segment holding neither of the run length pair
    /// </summary>
    /// <summary>
    /// The RLE array as a run of rows each entry covers, which the map draws to show the shape of the array
    /// </summary>
    public IReadOnlyList<RleRunDetail> RleRuns => _rleRuns ??= BuildRleRuns();

    private IReadOnlyList<RleRunDetail> BuildRleRuns()
    {
        if (Blob is not { } blob || !blob.Header.HasRleArray)
        {
            return [];
        }

        var runs = new List<RleRunDetail>(blob.RleEntries.Length);

        var row = 0;

        for (var i = 0; i < blob.RleEntries.Length; i++)
        {
            var entry = blob.RleEntries[i];

            var address = entry.PageSlot;

            var storeOrdinal = blob.VariableLengthData is { } store && address is { } located
                ? store.GetOrdinal(located.Page, located.Slot)
                : -1;

            runs.Add(new RleRunDetail(i,
                                      row,
                                      entry.Count,
                                      entry.IsValue,
                                      entry.IsValue ? entry.Value : entry.BitpackIndex,
                                      blob.Header.RleArrayOffset + (i * blob.Header.RleEntryBytes),
                                      address,
                                      storeOrdinal));

            row += entry.Count;
        }

        return runs;
    }

    /// <summary>
    /// The blob's regions in the order they sit on disk, which the hex gutter names while a drag is under way
    /// </summary>
    public IReadOnlyList<HexArea> HexAreas => _hexAreas ??= BuildHexAreas();

    private IReadOnlyList<HexArea> BuildHexAreas()
    {
        if (Blob is not { } blob)
        {
            return [];
        }

        var header = blob.Header;

        List<HexArea> areas =
        [
            new("Header", 0),
            new("Bookmarks", header.BookmarkArrayOffset),
            new("RLE Array", header.RleArrayOffset)
        ];

        if (header.IsVariableLengthData)
        {
            areas.Add(new HexArea("VLD", header.VariableLengthDataOffset));
        }
        else if (header.HasBitpackArray)
        {
            areas.Add(new HexArea("Bit Pack", header.BitpackArrayOffset));
        }

        return [.. areas.OrderBy(a => a.Start)];
    }

    /// <summary>
    /// Whether the page on show locates its values through an offset array rather than a fixed stride
    /// </summary>
    public bool HasVariableWidthValues => SelectedValuePage?.Page.IsVariableWidth ?? false;

    public string RleValueLabel => Blob?.Header.IsVariableLengthData == true ? "Repeat" : "Value";

    public string RleIndexLabel => Blob?.Header.IsVariableLengthData == true ? "Read" : "Bit Pack";

    public bool HasRleArray => Blob?.Header.HasRleArray ?? false;

    public bool HasBitpackArray => Blob?.Header.HasBitpackArray ?? false;

    /// <summary>
    /// Whether the segment holds a paged value store in place of the run length and bit pack pair
    /// </summary>
    public bool HasVariableLengthData => Blob?.VariableLengthData is not null;

    public ObservableCollection<ValuePageSummary> ValuePages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVariableWidthValues))]
    private ValuePageSummary? _selectedValuePage;

    [ObservableProperty]
    private ValueList? _values;

    partial void OnSelectedValuePageChanged(ValuePageSummary? value)
    {
        SelectedValue = null;

        Values = value is { } summary ? new ValueList(summary.Page) : null;

        SetPayload(value?.Page);

        if (value is not null)
        {
            GoToOffset(value.Offset);
        }
    }

    /// <summary>
    /// Hands the payload window the bytes the page expands to, and marks a value per row of it
    /// </summary>
    private void SetPayload(SegmentValuePage? page)
    {
        if (page is null)
        {
            PayloadHex.MarkerFactory = null;

            PayloadHex.SetData(default);

            return;
        }

        PayloadHex.MarkerFactory = (start, length) => BuildPayloadMarkers(page, start, length);

        PayloadHex.SetData(page.Values);

        PayloadHex.GoToOffset(0);
    }

    /// <summary>
    /// The selected value alone, a value being a fixed width slot of the expanded payload
    /// </summary>
    /// <remarks>
    /// One marker rather than one per value on show. A page runs to thousands of identical looking slots, so marking
    /// them all says nothing the fixed width does not already, and buries the one that was asked for.
    /// </remarks>
    private List<Marker> BuildPayloadMarkers(SegmentValuePage page, int start, int length)
    {
        var markers = new List<Marker>();

        // The offset array is what makes a variable width page readable at all, so it is marked whatever is selected
        if (page.IsVariableWidth)
        {
            Add(markers,
                "Offset Array",
                ItemType.ValueOffsetArray,
                page.OffsetArrayStart,
                page.OffsetArraySize,
                $"{page.ValueCount} Entries");
        }

        if (SelectedValue is not { } value)
        {
            return markers;
        }

        Add(markers,
            $"Value {value.Index}",
            ItemType.DictionaryValue,
            page.GetValuePosition(value.Index),
            page.GetValueLength(value.Index),
            value.StoredDescription);

        if (page.IsVariableWidth)
        {
            Add(markers,
                $"Offset {value.Index}",
                ItemType.ValueOffsetEntry,
                page.GetOffsetPosition(value.Index),
                2,
                $"0x{page.GetStoredOffset(value.Index):X4}");
        }

        return markers;

        void Add(List<Marker> into, string name, ItemType type, int position, int size, string text)
        {
            var offset = position - start;

            if (position < 0 || size <= 0 || offset < 0 || offset + size > length)
            {
                return;
            }

            into.Add(MarkerBuilder.CreateMarker(name, type, offset, size, text));
        }
    }

    /// <summary>
    /// The expanded payload of the selected page, which is where a value has a place of its own
    /// </summary>
    /// <remarks>
    /// A second window over a different run of bytes entirely - the blob hex above shows the page compressed, this
    /// shows what it decompresses to, so their offsets have nothing to do with one another.
    /// </remarks>
    public BlobHexViewModel PayloadHex { get; } = new();

    [ObservableProperty]
    private ValueDetail? _selectedValue;

    /// <summary>
    /// Picks out the page a value came from, that being as close as the hex can get to the value itself
    /// </summary>
    /// <remarks>
    /// The values sit inside a compressed payload, so an index has no range of the blob of its own. Selecting the
    /// page marker is what shows where it was read from.
    /// </remarks>
    public void SelectValue(ValueDetail? value)
    {
        SelectedValue = value;

        if (value is null || SelectedValuePage is not { } page)
        {
            return;
        }

        // The value has a place of its own in the expanded payload, a null having only its offset array entry
        var position = page.Page.GetValuePosition(value.Index);

        PayloadHex.GoToOffset(position >= 0 ? position : page.Page.GetOffsetPosition(value.Index));

        SelectPayloadMarker();
    }

    /// <summary>
    /// Moves the window onto an offset without changing which region tab is on show
    /// </summary>
    private void GoToOffset(int offset) => Hex.GoToOffset(offset);

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

    /// <summary>
    /// The whole segment blob, the regions being ranges within it rather than blobs of their own
    /// </summary>
    public BlobHexViewModel Hex { get; } = new();

    public ObservableCollection<SegmentElement> Elements { get; } = [];

    /// <summary>
    /// Every row of the segment, indexed rather than materialised so the grid only ever builds what it shows
    /// </summary>
    [ObservableProperty]
    private SegmentRowList? _rows;

    [ObservableProperty]
    private string _rowCountDescription = string.Empty;

    [ObservableProperty]
    private SegmentRowDetail? _selectedRow;

    /// <summary>
    /// Takes the row the grid picked, moves the window onto where its value was read from and marks it
    /// </summary>
    /// <remarks>
    /// A row stands for its ordinal, so the grid handing back an equal instance leaves the property unchanged and
    /// nothing would rebuild. The marker is for what is selected now, so it is built either way.
    /// </remarks>
    public void SelectRow(SegmentRowDetail? row)
    {
        SelectedRow = row;

        if (row is null || Blob is null)
        {
            Hex.BuildMarkers();

            return;
        }

        if (GetRowSource(row.Ordinal) is { } source)
        {
            GoToOffset(source.Offset);
        }
    }

    /// <summary>
    /// Where in the blob a row's value was read from, which differs by the store the segment uses
    /// </summary>
    /// <remarks>
    /// A store by value row can only be pointed at its page. The values there sit inside a compressed payload, so an
    /// individual one has no range of the blob to mark until the page has been expanded.
    /// </remarks>
    private (int Offset, int Length, string Name)? GetRowSource(int ordinal)
    {
        if (Blob is not { } blob || Rows is null)
        {
            return null;
        }

        if (blob.VariableLengthData is { } store)
        {
            var page = store.Pages[store.GetPageIndex(ordinal)];

            return (page.Offset, page.Size, $"Value Store Page {store.GetPageIndex(ordinal)}");
        }

        var source = (_dataIdStream ??= new SegmentDataIdStream(blob)).GetSource(ordinal);

        if (source.Origin == SegmentValueOrigin.BitPack)
        {
            var perUnit = blob.Bitpack.ValuesPerUnit;

            if (perUnit <= 0)
            {
                return null;
            }

            var unit = source.SourceIndex / perUnit;

            return (blob.Header.BitpackArrayOffset + (unit * BitpackArray.UnitBytes),
                    BitpackArray.UnitBytes,
                    $"Bit Pack Unit {unit}");
        }

        return (blob.Header.RleArrayOffset + (source.EntryIndex * blob.Header.RleEntryBytes),
                blob.Header.RleEntryBytes,
                $"RLE Entry {source.EntryIndex}");
    }

    /// <summary>
    /// The row on show, marked wherever the region markers put the window
    /// </summary>
    private IEnumerable<Marker> RowMarkers()
    {
        if (SelectedRow is not { } row || GetRowSource(row.Ordinal) is not { } source)
        {
            yield break;
        }

        yield return MarkerBuilder.CreateMarker(source.Name,
                                                ItemType.SegmentRowSource,
                                                source.Offset,
                                                source.Length,
                                                $"Row {row.Ordinal}");
    }

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
        }

        BitpackUnit = GetBitpackUnit(Hex.SelectedMarker);
    }

    [ObservableProperty]
    private int _selectedRegionTabIndex;

    private const int DataTabIndex = 5;

    [ObservableProperty]
    private bool _isDataTabLoaded;

    partial void OnSelectedRegionTabIndexChanged(int value)
    {
        if (value == DataTabIndex)
        {
            IsDataTabLoaded = true;
        }
    }

    [ObservableProperty]
    private int _selectedVariableLengthDataTabIndex;

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

        // A marker belongs to the region it was built for, so it means nothing once another region is on show
        Hex.SelectedMarker = null;

        GoToRegion(value);
    }

    /// <summary>
    /// Tab a region is shown on, so following something into another region brings its tab forward
    /// </summary>
    private static int GetRegionTabIndex(SegmentRegion region) => region switch
    {
        SegmentRegion.Bookmarks => 1,
        SegmentRegion.RleArray => 2,
        SegmentRegion.BitpackArray => 3,
        SegmentRegion.VariableLengthData => 4,
        _ => 0
    };

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
    /// Unit the bit ruler breaks apart, found from the marker selected in the bit pack region
    /// </summary>
    [ObservableProperty]
    private BitpackUnitDetail? _bitpackUnit;

    /// <summary>
    /// Hands the grid an indexed view over the segment, the rows themselves being worked out on demand
    /// </summary>
    private void BuildRows(SegmentBlob blob)
    {
        var stream = _dataIdStream ??= new SegmentDataIdStream(blob);

        var context = new SegmentRowContext(blob, stream, DeriveValue, IsDerivationVisible);

        Rows = new SegmentRowList(context, stream.RowCount);

        RowCountDescription = stream.RowCount == 1 ? "1 row" : $"{stream.RowCount} rows";
    }

    /// <summary>
    /// The pages of a store by value segment, there being none for any other layout
    /// </summary>
    private void BuildValuePages(SegmentBlob blob)
    {
        ValuePages.Clear();

        if (blob.VariableLengthData is not { } store)
        {
            return;
        }

        for (var i = 0; i < store.Pages.Length; i++)
        {
            ValuePages.Add(new ValuePageSummary
            {
                Index = i,
                Page = store.Pages[i],
                Offset = store.Pages[i].Offset,
                Size = store.Pages[i].Size
            });
        }

        SelectedValuePage = ValuePages.FirstOrDefault();
    }

    /// <summary>
    /// The working behind a row's value, which a wide store answers by ordinal rather than by data id
    /// </summary>
    private ValueDerivation? DeriveValue(int ordinal, long dataId)
    {
        if (Blob?.VariableLengthData is { IsWide: true } store)
        {
            return SegmentValueDerivation.BuildWide(Segment.Segment, store, ordinal);
        }

        return DeriveDataIdValue(dataId);
    }

    private ValueDerivation? DeriveDataIdValue(long dataId)
        => Decoder is { } decoder ? SegmentValueDerivation.Build(Segment.Segment, decoder, dataId) : null;

    private BitpackUnitDetail? GetBitpackUnit(Marker? marker)
    {
        if (Blob is not { } blob
            || blob.Header.IsVariableLengthData
            || Region != SegmentRegion.BitpackArray
            || marker is not { StartPosition: >= 0 })
        {
            return null;
        }

        var offset = Hex.HexBaseAddress + marker.StartPosition - blob.Header.BitpackArrayOffset;

        var index = offset / BitpackArray.UnitBytes;

        if (index < 0 || index >= blob.Header.BitpackUnitCount)
        {
            return null;
        }

        return BitpackUnitDetail.Build(blob, index, DeriveDataIdValue, IsDerivationVisible);
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

            Hex.GoToOffset(offset);
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
        Hex.WindowMoved -= OnWindowMoved;

        Hex.PropertyChanged -= OnHexPropertyChanged;

        Hex.Dispose();

        PayloadHex.Dispose();
    }

    /// <summary>
    /// Brings the region into line with the window, so a scroll past a boundary moves on to the tab it landed in
    /// </summary>
    /// <remarks>
    /// A jump is skipped because the region is what moved the window, and following it back would only fight the
    /// line alignment - a region starting part way into a line resolves to the region before it.
    /// </remarks>
    private void OnWindowMoved(object? sender, int start)
    {
        if (Blob is not { } blob || !IsAutoRegion || _isJumpingToRegion)
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

        Hex.BuildMarkers();
    }

    /// <summary>
    /// The bit ruler follows whatever the marker tree has picked, the marker now living on the hex view model
    /// </summary>
    private void OnHexPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BlobHexViewModel.SelectedMarker))
        {
            BitpackUnit = GetBitpackUnit(Hex.SelectedMarker);
        }
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

            Hex.WindowMoved -= OnWindowMoved;
            Hex.PropertyChanged -= OnHexPropertyChanged;

            Hex.MarkerFactory = (start, length) => BuildMarkers(blob, start, length);

            Hex.WindowMoved += OnWindowMoved;
            Hex.PropertyChanged += OnHexPropertyChanged;

            Hex.SetData(blob.Data);

            Elements.Clear();

            foreach (var element in SegmentElementBuilder.Build(blob))
            {
                Elements.Add(element);
            }

            BuildRows(blob);

            BuildValuePages(blob);

            Region = SegmentRegion.Header;

            SelectedRegionTabIndex = 0;

            SelectedVariableLengthDataTabIndex = 0;

            GoToRegion(SegmentRegion.Header);

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
    /// Everything the window holds, being the region on show plus the row picked in the data grid
    /// </summary>
    private List<Marker> BuildMarkers(SegmentBlob blob, int start, int length)
    {
        var rows = SegmentRegionMarkerBuilder.Window(RowMarkers(), start, length);

        if (Region != SegmentRegion.VariableLengthData || blob.VariableLengthData is not { } store)
        {
            return [.. SegmentRegionMarkerBuilder.Build(blob, Region, start, length), .. rows];
        }

        // Split so the store and the page picked in the list each have a tree of their own
        var header = SegmentRegionMarkerBuilder.Window(MarkerBuilder.BuildMarkers(store), start, length);

        var page = SelectedValuePage is { } selected
            ? SegmentRegionMarkerBuilder.Window(MarkerBuilder.BuildMarkers(selected.Page), start, length)
            : [];

        VariableLengthDataHeaderMarkers = new ObservableCollection<Marker>(header);

        ValuePageMarkers = new ObservableCollection<Marker>(page);

        return [.. header, .. page, .. rows];
    }

    /// <summary>
    /// Fields of the store itself, which the page list above the tabs does not stand for
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Marker> _variableLengthDataHeaderMarkers = [];

    [ObservableProperty]
    private ObservableCollection<Marker> _valuePageMarkers = [];

    /// <summary>
    /// Picks out the compressed payload on the blob hex, that being where the decode reads from
    /// </summary>
    /// <remarks>
    /// Matched on what the marker is rather than where it starts, the page header sharing its first byte with the
    /// page itself so an offset alone would find the sub lob type instead.
    /// </remarks>
    public void SelectPayloadMarker()
        => Hex.SelectedMarker = Hex.Markers.FirstOrDefault(m => m.Type == ItemType.ValuePagePayload);

    /// <summary>
    /// Moves the window onto the store header, which the tab showing its fields asks for
    /// </summary>
    public void GoToVariableLengthDataHeader()
    {
        if (Blob?.VariableLengthData is { } store)
        {
            Hex.GoToOffset(store.Offset);
        }
    }

    /// <summary>
    /// Moves to the item the target names, showing its region and selecting the marker over it
    /// </summary>
    /// <remarks>
    /// The markers are built rather than left to the debounce, there being nothing to select until they exist.
    /// </remarks>
    /// <summary>
    /// Follows a run to the bit pack entry it names, the unit holding it being what can actually be shown
    /// </summary>
    public void GoToBitpackValue(long valueIndex)
    {
        if (Blob is not { } blob || blob.Bitpack.ValuesPerUnit <= 0)
        {
            return;
        }

        var unit = (int)(valueIndex / blob.Bitpack.ValuesPerUnit);

        if (unit < 0 || unit >= blob.Header.BitpackUnitCount)
        {
            return;
        }

        BitpackUnit = BitpackUnitDetail.Build(blob, unit, DeriveDataIdValue, IsDerivationVisible);

        GoToTarget(new SegmentNavigationTarget(SegmentRegion.BitpackArray,
                                               blob.Header.BitpackArrayOffset + (unit * BitpackArray.UnitBytes)));
    }

    public void SelectRun(RleRunDetail? run)
    {
        if (run is not null)
        {
            GoToTarget(new SegmentNavigationTarget(SegmentRegion.RleArray, run.Offset));
        }
    }

    /// <summary>
    /// Follows a run to the value it names, which means the page holding it as well as the bytes
    /// </summary>
    /// <remarks>
    /// The store is addressed by page and slot, so showing the value means picking its page, turning to the tab
    /// that decodes one, and selecting the slot there. The page has to be picked first, choosing one rebuilding
    /// the value list the slot is taken from.
    /// </remarks>
    public void GoToValue(string address)
    {
        if (Blob?.VariableLengthData is not { } store || !SegmentPageSlot.TryParse(address, out var parsed))
        {
            return;
        }

        SelectedValuePage = ValuePages.FirstOrDefault(p => p.Index == parsed.Page);

        SelectedVariableLengthDataTabIndex = DecodeTabIndex;

        if (Values is { } values && parsed.Slot >= 0 && parsed.Slot < values.Count)
        {
            SelectValue(values[parsed.Slot]);
        }

        GoToTarget(new SegmentNavigationTarget(SegmentRegion.VariableLengthData,
                                               store.GetValueOffset(parsed.Page, parsed.Slot)));
    }

    private const int DecodeTabIndex = 2;

    public void GoToTarget(SegmentNavigationTarget target)
    {
        if (Blob is null)
        {
            return;
        }

        _isFollowingWindow = true;

        Region = target.Region;

        _isFollowingWindow = false;

        SelectedRegionTabIndex = GetRegionTabIndex(target.Region);

        Hex.GoToOffset(target.Offset);

        Hex.SelectedMarker = Hex.Markers.FirstOrDefault(m => m.StartPosition == target.Offset - Hex.HexBaseAddress);
    }
}
