using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Annotations;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Services.Markers;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

/// <summary>
/// One dictionary, its entries over the pages holding them and the coding those pages use
/// </summary>
public sealed partial class DictionaryTabViewModel(ColumnstoreService columnstoreService,
                                                   DatabaseSource database,
                                                   SegmentDictionary dictionary) : ObservableObject, IDisposable
{
    private const int SpinnerDelayMs = 100;

    private ColumnstoreService ColumnstoreService { get; } = columnstoreService;

    private DatabaseSource Database { get; } = database;

    public SegmentDictionary Dictionary { get; } = dictionary;

    [ObservableProperty]
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
    private string _statusText = "Loading dictionary...";

    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>
    /// Every entry, indexed rather than materialised so the grid only ever decodes what it shows
    /// </summary>
    [ObservableProperty]
    private DictionaryEntryList? _entries;

    public ObservableCollection<DictionaryPageSummary> Pages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tree))]
    [NotifyPropertyChangedFor(nameof(HasHuffmanPage))]
    private DictionaryPageSummary? _selectedPage;

    /// <summary>
    /// Whether the selected page carries a coding, an uncompressed page having no table or tree to show
    /// </summary>
    public bool HasHuffmanPage => SelectedPage?.Huffman is not null;

    public HuffmanTreeNode? Tree => SelectedPage?.Tree;

    /// <summary>
    /// Whether the blob is laid out in pages at all, a numeric dictionary holding a flat array of values instead
    /// </summary>
    public bool HasPages => Blob is StringDictionary;

    /// <summary>
    /// Whether the values sit in a flat array rather than in pages, which is what a numeric dictionary holds
    /// </summary>
    public bool HasValues => Blob is NumericDictionary;

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

        OnPropertyChanged(nameof(Codes));

        SelectedEntry = null;

        PageEntries = BuildPageEntries(value);

        if (value is not null && !_isLoading)
        {
            Hex.GoToOffset(value.Offset);
        }
    }

    /// <summary>
    /// Takes the entry the grid picked, and marks it whether or not the property saw a change
    /// </summary>
    /// <remarks>
    /// An entry stands for its index, so the grid handing back an equal instance leaves the property unchanged and
    /// nothing would rebuild. The markers are for what is selected now, so they are built either way.
    /// </remarks>
    public void SelectEntry(DictionaryEntryDetail? entry)
    {
        SelectedEntry = entry;

        SelectedStep = -1;

        Hex.BuildMarkers();
    }

    /// <summary>
    /// Narrows the entry list to the page, the handle array being what says which page an entry lives on
    /// </summary>
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

    /// <summary>
    /// Set while the load picks a first page, whose selection must not carry the window off the header
    /// </summary>
    /// <remarks>
    /// The window only ever holds the lines on screen, so moving it to a page leaves every header field outside it
    /// and nothing to mark. A blob opens on its header, and moves only where the reader sends it.
    /// </remarks>
    private bool _isLoading;

    /// <summary>
    /// Whether the grids show the working behind a value, or only the value itself
    /// </summary>
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
            var header = Window(MarkerBuilder.BuildMarkers(blob), start, length);

            var page = SelectedPage is { } selected
                ? Window([.. MarkerBuilder.BuildMarkers(selected.Page), .. EntryMarkers()], start, length)
                : Window([.. EntryMarkers()], start, length);

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

    public void Dispose() => Hex.Dispose();

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

            OnPropertyChanged(nameof(HasValues));

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
        var kind = blob is StringDictionary ? "String" : "Numeric";

        var pages = blob is StringDictionary strings ? $", {strings.Pages.Length} pages" : string.Empty;

        return $"{kind}, {blob.EntryCount} entries from Data Id {blob.FirstId}{pages}, "
               + $"{blob.LobType.ToString().SplitCamelCase()}";
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
