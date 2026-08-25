using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Blobs;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Controls.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Services.Markers;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

/// <summary>
/// One dictionary, its entries over the pages holding them and the coding those pages use
/// </summary>
public sealed partial class DictionaryTabViewModel(ColumnstoreService columnstoreService,
                                                   DatabaseSource database,
                                                   SegmentDictionary dictionary,
                                                   ColumnStoreColumn? column) : ObservableObject, IDisposable
{
    private const int SpinnerDelayMs = 100;

    private const float ShadeFactor = 0.72f;

    private const int MaxMarkedEntries = 10;

    private const int HandleFieldBytes = 4;

    private const int PageSizeBytes = 4;

    private ColumnstoreService ColumnstoreService { get; } = columnstoreService;

    private DatabaseSource Database { get; } = database;

    public SegmentDictionary Dictionary { get; } = dictionary;

    public ColumnStoreColumn? Column { get; } = column;

    public string ColumnName => Column?.Name ?? $"Column {Dictionary.ColumnId}";

    /// <summary>
    /// The type the column was declared as, which the header shows beside its name
    /// </summary>
    /// <remarks>
    /// Surfaced a field at a time because a data type is drawn from four of them, and x:Bind cannot reach through a
    /// structure that may not be there.
    /// </remarks>
    public SqlDbType? DataType => Column?.Structure?.DataType;

    public int Precision => Column?.Structure?.Precision ?? 0;

    public int Scale => Column?.Structure?.Scale ?? 0;

    public int DataLength => Column?.Structure?.DataLength ?? 0;

    /// <summary>
    /// Whether the dictionary covers the whole index or the one row group, which the header shows under the column
    /// </summary>
    public IReadOnlyList<SegmentBadge> ScopeBadges =>
    [
        Dictionary.IsGlobal
            ? SegmentBadge.Create("Global", ColumnstoreColours.GlobalScope)
            : SegmentBadge.Create($"Local {Dictionary.DictionaryId}", ColumnstoreColours.LocalScope)
    ];

    /// <summary>
    /// The lob type and the store it opens, the store being the element that tells one dictionary kind from the other
    /// </summary>
    public IReadOnlyList<SegmentBadge> TypeBadges
    {
        get
        {
            var colour = ColumnstoreLayout.GetDictionaryColour(Dictionary.Type);

            var badges = new List<SegmentBadge>
            {
                SegmentBadge.Create($"{ColumnstoreLayout.GetDictionaryTypeDescription(Dictionary.Type)} Dictionary",
                                    colour)
            };

            if (StoreSubLobType is { } store)
            {
                badges.Add(SegmentBadge.Create(store.ToString().SplitCamelCase(),
                                               ColumnstoreColours.Shade(colour, ShadeFactor)));
            }

            return SegmentBadge.Compound(badges);
        }
    }

    private SubLobType? StoreSubLobType => Blob switch
    {
        NumericDictionary numeric => numeric.HashTable.SubLobType,
        StringDictionary strings => strings.Store.SubLobType,
        _ => null
    };

    /// <summary>
    /// What the blob turned out to hold, which is only known once it is read
    /// </summary>
    public IReadOnlyList<SegmentBadge> FlagBadges => SegmentBadge.Compound([.. BuildFlagBadges()]);

    private IEnumerable<SegmentBadge> BuildFlagBadges()
    {
        if (Blob is not StringDictionary strings || strings.Pages.Length == 0)
        {
            yield break;
        }

        var huffman = strings.Pages.Count(p => p is HuffmanStringPage);

        yield return huffman switch
        {
            0 => SegmentBadge.Create("Uncompressed", ColumnstoreColours.UncompressedFlag),
            _ when huffman == strings.Pages.Length => SegmentBadge.Create("Huffman", ColumnstoreColours.HuffmanFlag),
            _ => SegmentBadge.Create($"Huffman {huffman}/{strings.Pages.Length}", ColumnstoreColours.HuffmanFlag)
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlagBadges))]
    [NotifyPropertyChangedFor(nameof(TypeBadges))]
    private DictionaryBlob? _blob;

    /// <summary>
    /// The whole dictionary blob, a page being a range within it rather than a blob of its own
    /// </summary>
    public BlobHexViewModel Hex { get; } = new();

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private bool _isDictionaryLoading;

    [ObservableProperty]
    private string _statusText = "Loading Dictionary...";

    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>
    /// Every entry, indexed rather than materialised so the grid only ever decodes what it shows
    /// </summary>
    [ObservableProperty]
    private DictionaryEntryList? _entries;

    public ObservableCollection<DictionaryPageSummary> Pages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHuffmanPage))]
    [NotifyPropertyChangedFor(nameof(EntryValueItemType))]
    [NotifyPropertyChangedFor(nameof(EntryLengthItemType))]
    private DictionaryPageSummary? _selectedPage;

    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// Region the window sits on, set by picking a tab and reported back when a scroll leaves the region
    /// </summary>
    [ObservableProperty]
    private DictionaryRegion _region = DictionaryRegion.Header;

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

    partial void OnRegionChanged(DictionaryRegion value)
    {
        if (_isFollowingWindow)
        {
            return;
        }

        // A marker belongs to the region it was built for, so it means nothing once another region is on show
        Hex.SelectedMarker = null;

        GoToRegion(value);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (_isFollowingWindow)
        {
            return;
        }

        Hex.SelectedMarker = null;

        var region = GetTabRegion(value);

        if (region == Region)
        {
            GoToRegion(region);

            return;
        }

        Region = region;
    }

    /// <summary>
    /// Region a tab shows, so picking one moves the window to what it describes
    /// </summary>
    private static DictionaryRegion GetTabRegion(int index) => index switch
    {
        1 => DictionaryRegion.Handles,
        2 => DictionaryRegion.Pages,
        3 => DictionaryRegion.Values,
        _ => DictionaryRegion.Header
    };

    /// <summary>
    /// Tab a region is shown on, so following the window into another region brings its tab forward
    /// </summary>
    private static int GetRegionTabIndex(DictionaryRegion region) => region switch
    {
        DictionaryRegion.Handles => 1,
        DictionaryRegion.Pages => 2,
        DictionaryRegion.Values => 3,
        _ => 0
    };

    /// <summary>
    /// Moves the window to the region's first line, or rebuilds in place if it is already there
    /// </summary>
    private void GoToRegion(DictionaryRegion region)
    {
        if (Blob is not { } blob)
        {
            return;
        }

        _isJumpingToRegion = true;

        try
        {
            Hex.GoToOffset(DictionaryRegions.GetOffset(blob, region) / BlobHexViewModel.BytesPerLine
                           * BlobHexViewModel.BytesPerLine);
        }
        finally
        {
            _isJumpingToRegion = false;
        }
    }

    /// <summary>
    /// Brings the region into line with the window, so a scroll past a boundary moves on to the tab it landed in
    /// </summary>
    private void OnWindowMoved(object? sender, int start)
    {
        if (Blob is not { } blob || !IsAutoRegion || _isJumpingToRegion)
        {
            return;
        }

        var region = DictionaryRegions.GetRegion(blob, start);

        if (region == Region)
        {
            return;
        }

        _isFollowingWindow = true;

        Region = region;

        SelectedTabIndex = GetRegionTabIndex(region);

        _isFollowingWindow = false;

        Hex.BuildMarkers();
    }

    [ObservableProperty]
    private int _selectedPageTabIndex;

    [ObservableProperty]
    private bool _isPageLoading;

    /// <summary>
    /// Whether the selected page carries a coding, an uncompressed page having no table or tree to show
    /// </summary>
    public bool HasHuffmanPage => SelectedPage?.Huffman is not null;

    /// <summary>
    /// Colour the selected entry's value is marked in, a coded entry being marked over the words its bits fall in
    /// </summary>
    public ItemType EntryValueItemType => Blob is NumericDictionary
        ? ItemType.DictionaryValue
        : HasHuffmanPage
            ? ItemType.StringEntryCode
            : ItemType.StringEntryValue;

    /// <summary>
    /// Colour the entry's length prefix is marked in, which only a page holding its values plainly carries
    /// </summary>
    public ItemType EntryLengthItemType => Blob is StringDictionary && !HasHuffmanPage
        ? ItemType.StringEntryLength
        : ItemType.None;

    /// <summary>
    /// Which halves of the decode the reader wants, the entries read out and the coding that produced them
    /// </summary>
    /// <remarks>
    /// Turning the last one off would leave the tab blank, so the other comes back on rather than being refused.
    /// </remarks>
    [ObservableProperty]
    private bool _isDecodeValuesVisible = true;

    [ObservableProperty]
    private bool _isDecodeDetailsVisible = true;

    partial void OnIsDecodeValuesVisibleChanged(bool value)
    {
        if (!value && !IsDecodeDetailsVisible)
        {
            IsDecodeDetailsVisible = true;
        }
    }

    partial void OnIsDecodeDetailsVisibleChanged(bool value)
    {
        if (!value && !IsDecodeValuesVisible)
        {
            IsDecodeValuesVisible = true;
        }
    }

    public HuffmanTreeNode? Tree => SelectedPage?.Tree;

    public bool HasPages => Blob is StringDictionary;

    public bool HasValues => Blob is NumericDictionary;

    public bool HasHandles => Blob is StringDictionary;

    /// <summary>
    /// The handle array, listed lazily, a dictionary running to tens of thousands of entries
    /// </summary>
    public DictionaryHandleList? Handles => Blob is StringDictionary strings ? new DictionaryHandleList(strings) : null;

    /// <summary>
    /// Moves the window onto the handle itself, which is where the lookup starts rather than where it lands
    /// </summary>
    public void SelectHandle(DictionaryHandleDetail? handle)
    {
        SelectedHandle = handle;

        if (handle is not null)
        {
            Hex.GoToOffset(handle.HandleOffset);
        }

        Hex.BuildMarkers();
    }

    [ObservableProperty]
    private DictionaryHandleDetail? _selectedHandle;

    /// <summary>
    /// The fields of the handle on show, which the array is too long to open for every entry
    /// </summary>
    private IEnumerable<Marker> SelectedHandleMarkers()
    {
        if (SelectedHandle is not { } handle || Blob is not StringDictionary)
        {
            yield break;
        }

        yield return MarkerBuilder.CreateMarker("Offset",
                                                ItemType.DictionaryHandleOffset,
                                                handle.HandleOffset,
                                                4,
                                                $"{handle.Offset}");

        yield return MarkerBuilder.CreateMarker("Page",
                                                ItemType.DictionaryHandlePage,
                                                handle.HandleOffset + 4,
                                                4,
                                                $"{handle.Page}");
    }

    /// <summary>
    /// The blob's parts in the order they sit, which the hex gutter names while a drag is under way
    /// </summary>
    public IReadOnlyList<HexArea> HexAreas
    {
        get
        {
            switch (Blob)
            {
                case NumericDictionary:
                    return
                    [
                        new HexArea("Dictionary Header", 0),
                        new HexArea("Hash Table", 0x0C),
                        new HexArea("Array Header", 0x2C),
                        new HexArea("Values", NumericDictionary.HeaderSize)
                    ];

                case StringDictionary strings:
                    var pageSizes = StringDictionary.HandleArrayOffset + (strings.HandleCount * strings.HandleSize);

                    return
                    [
                        new HexArea("Dictionary Header", 0),
                        new HexArea("String Store", 0x0C),
                        new HexArea("Handle Array Header", StringDictionary.HandleArrayHeaderOffset),
                        new HexArea("Page Size Array Header", StringDictionary.PageSizeArrayHeaderOffset),
                        new HexArea("Handles", StringDictionary.HandleArrayOffset),
                        new HexArea("Page Sizes", pageSizes),
                        new HexArea("Pages", pageSizes + (strings.PageCount * PageSizeBytes))
                    ];

                default:
                    return [];
            }
        }
    }

    public string GetCsIndexCommand(int printMode)
        => CsIndexCommand.Build(Dictionary, Database.DatabaseId, Dictionary.HobtId, printMode);

    /// <summary>
    /// Entries living on the selected page, which is the list the decode walks through
    /// </summary>
    [ObservableProperty]
    private DictionaryEntryList? _pageEntries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecodeSteps))]
    [NotifyPropertyChangedFor(nameof(DecodeStepDetails))]
    [NotifyPropertyChangedFor(nameof(DecodeSummary))]
    [NotifyPropertyChangedFor(nameof(DecodeContent))]
    private DictionaryEntryDetail? _selectedEntry;

    [ObservableProperty]
    private int _selectedStep = -1;

    private IReadOnlyList<HuffmanDecodeStep>? _decodeSteps;

    private IReadOnlyList<DecodeStepDetail>? _decodeStepDetails;

    private IReadOnlyList<HuffmanCodeDetail>? _codes;

    /// <summary>
    /// The bit walk the selected entry decodes through, which only a Huffman coded page has
    /// </summary>
    public IReadOnlyList<HuffmanDecodeStep> DecodeSteps => _decodeSteps ??= Trace();

    /// <summary>
    /// The coded stream the walk reads from, which the drawing shows the words and bits of
    /// </summary>
    public ReadOnlyMemory<byte> DecodeContent => SelectedPage?.Huffman?.Content ?? default;

    /// <summary>
    /// Selects the code a clicked band used, so the table and the drawing stay on the same symbol
    /// </summary>
    public void SelectSymbol(int symbol) => SelectedSymbol = symbol;

    public IReadOnlyList<DecodeStepDetail> DecodeStepDetails
        => _decodeStepDetails ??= [.. DecodeSteps.Select((s, i) => new DecodeStepDetail { Step = s, Ordinal = i })];

    /// <summary>
    /// What the entry cost, being the bits it occupies against the bytes it decodes to
    /// </summary>
    public string DecodeSummary
    {
        get
        {
            if (SelectedEntry is not { } entry || SelectedPage is not { } page)
            {
                return string.Empty;
            }

            if (page.Huffman is null)
            {
                return $"{entry.Value.Length} characters at offset {entry.OffsetDescription}";
            }

            var steps = DecodeSteps;

            var bits = steps.Count == 0 ? 0 : steps[^1].BitOffset + steps[^1].BitLength - steps[0].BitOffset;

            return $"{bits} bits over {steps.Count} symbols, decoding to {entry.Value.Length} characters";
        }
    }

    private IReadOnlyList<HuffmanDecodeStep> Trace()
    {
        if (SelectedPage?.Huffman is not { } huffman || SelectedEntry is null || Blob is not StringDictionary strings)
        {
            return [];
        }

        try
        {
            return huffman.Trace(strings.Handles[SelectedEntry.Index].Offset);
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Codes of the selected page, which only a Huffman coded page carries
    /// </summary>
    public IReadOnlyList<HuffmanCodeDetail> Codes
        => _codes ??= SelectedPage?.Codes.Select(c => new HuffmanCodeDetail { Code = c }).ToList() ?? [];

    [ObservableProperty]
    private int _selectedSymbol = -1;

    partial void OnSelectedEntryChanged(DictionaryEntryDetail? value) => ClearDecodeCache();

    partial void OnBlobChanged(DictionaryBlob? value)
    {
        _codes = null;

        ClearDecodeCache();
    }

    private void ClearDecodeCache()
    {
        _decodeSteps = null;

        _decodeStepDetails = null;
    }

    partial void OnSelectedPageChanged(DictionaryPageSummary? value)
    {
        SelectedSymbol = -1;

        _codes = null;

        ClearDecodeCache();

        SelectedEntry = null;

        _pageLoad = ApplySelectedPageAsync(value);

        if (value is not null && !_isLoading)
        {
            Hex.GoToOffset(value.Offset);
        }
    }

    private Task? _pageLoad;

    private const int DecodeTabIndex = 1;

    /// <summary>
    /// Follows a handle to the value it names, which is an entry of the page the handle points at
    /// </summary>
    public async Task GoToHandleValue(DictionaryHandleDetail handle)
    {
        if (Blob is not StringDictionary)
        {
            return;
        }

        SelectedTabIndex = GetRegionTabIndex(DictionaryRegion.Pages);

        if (Pages.FirstOrDefault(p => p.Index == handle.Page) is not { } page)
        {
            return;
        }

        SelectedPage = page;

        SelectedPageTabIndex = DecodeTabIndex;

        if (_pageLoad is { } load)
        {
            await load;
        }

        SelectEntry(PageEntries?.Find(handle.Index));
    }

    private async Task ApplySelectedPageAsync(DictionaryPageSummary? page)
    {
        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowPageSpinnerAfterDelay(spinnerDelay.Token);

        try
        {
            var detail = await Task.Run(() => LoadPageDetail(page));

            if (!ReferenceEquals(SelectedPage, page))
            {
                return;
            }

            PageEntries = detail.Entries;

            _codes = detail.Codes;

            OnPropertyChanged(nameof(Codes));

            OnPropertyChanged(nameof(Tree));
        }
        catch (Exception exception)
        {
            SummaryText = $"Page load failed: {exception.Message}";
        }
        finally
        {
            await spinnerDelay.CancelAsync();

            IsPageLoading = false;
        }
    }

    private (DictionaryEntryList? Entries, IReadOnlyList<HuffmanCodeDetail> Codes) LoadPageDetail(DictionaryPageSummary? page)
    {
        if (page is null)
        {
            return (null, []);
        }

        var codes = page.Codes.Select(c => new HuffmanCodeDetail { Code = c }).ToList();

        _ = page.Tree;

        return (BuildPageEntries(page), codes);
    }

    private async Task ShowPageSpinnerAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(SpinnerDelayMs, token);

            if (!token.IsCancellationRequested)
            {
                IsPageLoading = true;
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    public void SelectEntry(DictionaryEntryDetail? entry)
    {
        SelectedEntry = entry;

        SelectedStep = -1;

        if (GetEntryOffset(entry) is { } offset)
        {
            Hex.GoToOffset(offset);
        }

        Hex.BuildMarkers();
    }

    /// <summary>
    /// Where an entry's bytes start, a numeric one being an element and a string one whatever its page holds
    /// </summary>
    /// <remarks>
    /// A Huffman coded entry is addressed in bits, so the offset it lands on is the byte those bits fall in.
    /// </remarks>
    private int? GetEntryOffset(DictionaryEntryDetail? entry)
    {
        if (entry is null)
        {
            return null;
        }

        if (entry.ValueOffset >= 0)
        {
            return entry.ValueOffset;
        }

        if (SelectedPage is not { } page || Blob is not StringDictionary strings)
        {
            return null;
        }

        var handle = strings.Handles[entry.Index];

        return page.Page switch
        {
            UncompressedStringPage uncompressed => uncompressed.GetExtent(handle.Offset).Offset,
            _ => page.Offset + (handle.Offset / 8)
        };
    }

    private DictionaryEntryList? BuildPageEntries(DictionaryPageSummary? page)
    {
        if (page is null || Blob is not StringDictionary strings)
        {
            return null;
        }

        var indexes = new List<int>();

        for (var i = 0; i < strings.Handles.Length; i++)
        {
            if (strings.Handles[i].Page == page.Index)
            {
                indexes.Add(i);
            }
        }

        return new DictionaryEntryList(strings, IsDerivationVisible, [.. indexes]);
    }

    private bool _isLoading;

    [ObservableProperty]
    private bool _isDerivationVisible = true;

    partial void OnIsDerivationVisibleChanged(bool value)
    {
        if (Blob is { } blob)
        {
            Entries = new DictionaryEntryList(blob, value);
        }
    }

    /// <summary>
    /// Fields of the blob header, which the Header tab lists on its own
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Marker> _headerMarkers = [];

    /// <summary>
    /// Fields of the page on show together with the entry selected in it
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Marker> _pageMarkers = [];

    /// <summary>
    /// Splits the fields by what they describe, and hands the hex view the two of them together
    /// </summary>
    /// <remarks>
    /// The lists share their markers with the combined one rather than copying them, so a marker picked in either
    /// tree is the same object the hex view is holding and still highlights.
    /// </remarks>
    private List<Marker> BuildMarkers(DictionaryBlob blob, int start, int length)
    {
        try
        {
            var header = GroupHeader(Window([.. MarkerBuilder.BuildMarkers(blob), .. ArrayMarkers()], start, length));

            var page = SelectedPage is { } selected
                ? Window([.. MarkerBuilder.BuildMarkers(selected.Page), .. EntryMarkers(), .. SelectedHandleMarkers()],
                         start, length)
                : Window([.. EntryMarkers(), .. SelectedHandleMarkers()], start, length);

            HeaderMarkers = new ObservableCollection<Marker>(header);

            PageMarkers = new ObservableCollection<Marker>(page);

            return [.. header, .. page];
        }
        catch (Exception exception)
        {
            // The debounce that usually calls this is fire and forget, so a throw here would otherwise go unseen
            SummaryText = $"Markers failed: {exception.Message}";

            return [];
        }
    }

    /// <summary>
    /// Gathers the fields the blob opens with under one heading, the stores after them being grouped already
    /// </summary>
    /// <remarks>
    /// CSINDEX reports the first twelve bytes as the dictionary header and everything past them as a store of its
    /// own, so the tree reads the same way its output does.
    /// </remarks>
    private static List<Marker> GroupHeader(List<Marker> markers)
    {
        var loose = markers.Where(m => m.Children.Count == 0 && m.EndPosition < DictionaryHeaderSize).ToList();

        if (loose.Count == 0)
        {
            return markers;
        }

        var from = loose.Min(m => m.StartPosition);

        var section = MarkerBuilder.CreateMarker("Dictionary Header",
                                                 ItemType.SegmentHeaderSection,
                                                 from,
                                                 loose.Max(m => m.EndPosition) - from + 1,
                                                 string.Empty);

        section.Children = new ObservableCollection<Marker>(loose);

        return [section, .. markers.Except(loose)];
    }

    private const int DictionaryHeaderSize = 12;

    /// <summary>
    /// Positions the fields the window holds, and takes the position off the ones it does not
    /// </summary>
    /// <remarks>
    /// A field outside the window keeps its place in the tree and loses its position, the same as a field marked for
    /// context. Dropping it instead would empty the tree whenever the window sat anywhere but the header.
    /// </remarks>
    private static List<Marker> Window(IEnumerable<Marker> markers, int start, int length)
    {
        var windowed = new List<Marker>();

        var end = start + length - 1;

        foreach (var marker in markers)
        {
            // Clipped rather than dropped, a coded entry running well past whatever the window happens to hold
            var from = Math.Max(marker.StartPosition, start);

            var to = Math.Min(marker.EndPosition, end);

            if (marker.StartPosition < 0 || to < from)
            {
                marker.StartPosition = -1;
                marker.EndPosition = -1;
            }
            else
            {
                marker.StartPosition = from - start;
                marker.EndPosition = to - start;
            }

            windowed.Add(marker);
        }

        return windowed;
    }

    /// <summary>
    /// The arrays the headers describe, which are data rather than fields and so are not marked as the blob is parsed
    /// </summary>
    /// <remarks>
    /// A dictionary runs to tens of thousands of entries, so past a handful the run is marked as one region instead.
    /// Marking every entry would flood the tree, and the entry a reader wants is the one they select in the grid.
    /// </remarks>
    private IEnumerable<Marker> ArrayMarkers()
    {
        switch (Blob)
        {
            case NumericDictionary numeric when numeric.ValueCount > 0 && numeric.ElementSize > 0:
                yield return MarkerBuilder.CreateMarker("Value Array",
                                                        ItemType.DictionaryValue,
                                                        NumericDictionary.HeaderSize,
                                                        numeric.ValueCount * numeric.ElementSize,
                                                        $"({numeric.ValueCount} Entries)");

                break;

            case StringDictionary strings:
                foreach (var marker in HandleMarkers(strings))
                {
                    yield return marker;
                }

                foreach (var marker in MarkRegion("Page Size",
                                                  ItemType.DictionaryPageSize,
                                                  StringDictionary.HandleArrayOffset
                                                  + (strings.HandleCount * strings.HandleSize),
                                                  strings.PageCount,
                                                  PageSizeBytes,
                                                  i => $"{strings.PageSizes[i]} bytes"))
                {
                    yield return marker;
                }

                break;
        }
    }

    /// <summary>
    /// Handles carry two fields, so each one that is marked on its own opens to show them
    /// </summary>
    private static IEnumerable<Marker> HandleMarkers(StringDictionary strings)
    {
        var markers = MarkRegion("Handle",
                                 ItemType.DictionaryHandle,
                                 StringDictionary.HandleArrayOffset,
                                 strings.HandleCount,
                                 strings.HandleSize,
                                 _ => string.Empty);

        if (strings.HandleCount > MaxMarkedEntries)
        {
            return markers;
        }

        return markers.Select((marker, index) =>
        {
            var handle = strings.Handles[index];

            marker.Children =
            [
                MarkerBuilder.CreateMarker("Offset",
                                           ItemType.DictionaryHandleOffset,
                                           marker.StartPosition,
                                           HandleFieldBytes,
                                           $"{handle.Offset}"),
                MarkerBuilder.CreateMarker("Page",
                                           ItemType.DictionaryHandlePage,
                                           marker.StartPosition + HandleFieldBytes,
                                           HandleFieldBytes,
                                           $"{handle.Page}")
            ];

            return marker;
        });
    }

    /// <summary>
    /// One marker per entry while there are few enough to read, and one over the whole run once there are not
    /// </summary>
    private static IEnumerable<Marker> MarkRegion(string name,
                                                  ItemType type,
                                                  int offset,
                                                  int count,
                                                  int elementSize,
                                                  Func<int, string> describe)
    {
        if (count <= 0 || elementSize <= 0)
        {
            yield break;
        }

        if (count > MaxMarkedEntries)
        {
            yield return MarkerBuilder.CreateMarker($"{name} Array",
                                                    type,
                                                    offset,
                                                    count * elementSize,
                                                    $"({count} Entries)");

            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            yield return MarkerBuilder.CreateMarker($"{name} {i}",
                                                    type,
                                                    offset + (i * elementSize),
                                                    elementSize,
                                                    describe(i));
        }
    }

    /// <summary>
    /// The selected entry as it sits in its page, which the parser cannot mark because it depends on the selection
    /// </summary>
    /// <remarks>
    /// An uncompressed entry is a length prefix and the bytes it counts. A coded one has no byte boundaries of its
    /// own, so the whole run of words its bits fall in is marked instead and the bit walk shows the detail.
    /// </remarks>
    private IEnumerable<Marker> EntryMarkers()
    {
        if (SelectedEntry is not { } entry)
        {
            yield break;
        }

        if (Blob is NumericDictionary)
        {
            yield return MarkerBuilder.CreateMarker("Value",
                                                    ItemType.DictionaryValue,
                                                    entry.ValueOffset,
                                                    entry.ValueSize,
                                                    entry.Value);

            yield break;
        }

        if (SelectedPage is not { } page || Blob is not StringDictionary strings)
        {
            yield break;
        }

        var handle = strings.Handles[entry.Index];

        if (page.Page is UncompressedStringPage uncompressed)
        {
            var extent = uncompressed.GetExtent(handle.Offset);

            yield return MarkerBuilder.CreateMarker("Entry Length",
                                                    ItemType.StringEntryLength,
                                                    extent.Offset,
                                                    extent.PrefixLength,
                                                    $"{extent.Length} bytes");

            yield return MarkerBuilder.CreateMarker("Entry Value",
                                                    ItemType.StringEntryValue,
                                                    extent.ValueOffset,
                                                    extent.Length,
                                                    entry.Value);

            yield break;
        }

        var steps = DecodeSteps;

        if (page.Huffman is null || steps.Count == 0)
        {
            yield break;
        }

        var contentStart = page.Offset + HuffmanStringPage.DataOffset;

        var firstBit = steps[0].BitOffset;

        var lastBit = steps[^1].BitOffset + steps[^1].BitLength;

        var start = contentStart + (firstBit / 16 * 2);

        var end = contentStart + (((lastBit - 1) / 16 * 2) + 2);

        yield return MarkerBuilder.CreateMarker("Coded Entry",
                                                ItemType.StringEntryCode,
                                                start,
                                                end - start,
                                                $"{lastBit - firstBit} bits from bit {firstBit}");
    }

    public void Dispose()
    {
        Hex.WindowMoved -= OnWindowMoved;

        Hex.Dispose();
    }

    public async Task Load(CancellationToken cancellationToken)
    {
        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowSpinnerAfterDelay(spinnerDelay.Token);

        _isLoading = true;

        try
        {
            var blob = await Task.Run(
                () => ColumnstoreService.GetDictionaryBlob(Database, Dictionary, cancellationToken, isMarkEnabled: true),
                cancellationToken);

            await spinnerDelay.CancelAsync();

            Blob = blob;

            OnPropertyChanged(nameof(HasPages));

            OnPropertyChanged(nameof(EntryValueItemType));

            OnPropertyChanged(nameof(EntryLengthItemType));

            OnPropertyChanged(nameof(HasValues));

            OnPropertyChanged(nameof(HasHandles));

            OnPropertyChanged(nameof(Handles));

            Hex.WindowMoved -= OnWindowMoved;

            Hex.WindowMoved += OnWindowMoved;

            Hex.MarkerFactory = (start, length) => BuildMarkers(blob, start, length);

            Hex.SetData(blob.Data);

            Entries = new DictionaryEntryList(blob, IsDerivationVisible);

            Pages.Clear();

            if (blob is StringDictionary strings)
            {
                for (var i = 0; i < strings.Pages.Length; i++)
                {
                    Pages.Add(new DictionaryPageSummary { Index = i, Page = strings.Pages[i] });
                }

                SelectedPage = Pages.FirstOrDefault(p => p.Huffman is not null) ?? Pages.FirstOrDefault();
            }

            SummaryText = Describe(blob);

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

            IsDictionaryLoading = false;

            _isLoading = false;
        }
    }

    /// <summary>
    /// Selects the entry a data id addresses, for a caller arriving from a segment
    /// </summary>
    public void GoToDataId(long dataId)
    {
        if (Blob is not { } blob)
        {
            return;
        }

        var index = (int)(dataId - blob.FirstId);

        SelectedEntryIndex = index >= 0 && index < blob.EntryCount ? index : -1;
    }

    [ObservableProperty]
    private int _selectedEntryIndex = -1;

    private static string Describe(DictionaryBlob blob)
    {
        var pages = blob is StringDictionary strings ? $", {strings.Pages.Length} pages" : string.Empty;

        return $"{blob.EntryCount} entries, Data Id {blob.FirstId} to {blob.FirstId + blob.EntryCount - 1}"
               + $"{pages}, {blob.Data.Length} bytes";
    }

    private async Task ShowSpinnerAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(SpinnerDelayMs, token);

            if (!token.IsCancellationRequested)
            {
                IsDictionaryLoading = true;
            }
        }
        catch (TaskCanceledException)
        {
            // The dictionary read finished inside the delay, so no spinner is wanted
        }
    }
}
