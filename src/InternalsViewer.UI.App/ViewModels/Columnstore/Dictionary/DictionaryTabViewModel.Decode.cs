using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;
using InternalsViewer.UI.App.Services.Markers;
using InternalsViewer.UI.App.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Columnstore.Dictionary;

/// <summary>
/// Columnstore Dictionary Tab View Model - Decode Tab
/// </summary>
public sealed partial class DictionaryTabViewModel
{
    public ObservableCollection<DictionaryPageSummary> Pages { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHuffmanPage))]
    [NotifyPropertyChangedFor(nameof(EntryValueItemType))]
    [NotifyPropertyChangedFor(nameof(EntryLengthItemType))]
    private DictionaryPageSummary? _selectedPage;

    [ObservableProperty]
    private bool _isPageLoading;

    public bool HasHuffmanPage => SelectedPage?.Huffman is not null;

    public ItemType EntryValueItemType => Blob is NumericDictionary
                                          ? ItemType.DictionaryValue
                                          : HasHuffmanPage
                                              ? ItemType.StringEntryCode
                                              : ItemType.StringEntryValue;

    public ItemType EntryLengthItemType => Blob is StringDictionary && !HasHuffmanPage
        ? ItemType.StringEntryLength
        : ItemType.None;

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

    public DictionaryHandleList? Handles => Blob is StringDictionary strings
                                            ? _handles ??= new DictionaryHandleList(strings)
                                            : null;

    private DictionaryHandleList? _handles;

    public void SelectHandle(DictionaryHandleDetail? handle)
    {
        SelectedHandle = handle;

        if (handle is not null)
        {
            Hex.GoToOffset(handle.HandleOffset);
        }

        Hex.BuildMarkers();

        SelectMarker(handle is null ? null : ItemType.DictionaryHandle);
    }

    /// <summary>
    /// Puts the mask back on the row that is picked, the markers having been replaced by a rebuild
    /// </summary>
    /// <remarks>
    /// A window move drops the selection along with the markers it was made against. The row picked in a table
    /// outlives them both, so the mask is put back from whatever that row is once the new markers arrive.
    /// </remarks>
    private void OnHexPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BlobHexViewModel.Markers) || Hex.SelectedMarker is not null)
        {
            return;
        }

        if (IsHandleSelectionShown && SelectedHandle is not null)
        {
            SelectMarker(ItemType.DictionaryHandle);
        }
        else if (IsEntrySelectionShown && SelectedEntry is not null)
        {
            SelectMarker(GetEntryItemType());
        }
    }

    /// <summary>
    /// Puts the mask on what was just picked, the markers having been rebuilt from scratch to show it
    /// </summary>
    private void SelectMarker(ItemType? type)
        => Hex.SelectedMarker = type is { } wanted ? MarkerLookup.FindByType(Hex.Markers, wanted) : null;

    [ObservableProperty]
    private DictionaryHandleDetail? _selectedHandle;

    [ObservableProperty]
    private DictionaryEntryList? _pageEntries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DecodeSteps))]
    [NotifyPropertyChangedFor(nameof(DecodeStepDetails))]
    [NotifyPropertyChangedFor(nameof(DecodeContent))]
    private DictionaryEntryDetail? _selectedEntry;

    [ObservableProperty]
    private int _selectedStep = -1;

    private IReadOnlyList<HuffmanDecodeStep>? _decodeSteps;

    private IReadOnlyList<DecodeStepDetail>? _decodeStepDetails;

    private IReadOnlyList<HuffmanCodeDetail>? _codes;

    public IReadOnlyList<HuffmanDecodeStep> DecodeSteps => _decodeSteps ??= Trace();

    public ReadOnlyMemory<byte> DecodeContent => SelectedPage?.Huffman?.Content ?? default;

    public void SelectSymbol(int symbol) => SelectedSymbol = symbol;

    public IReadOnlyList<DecodeStepDetail> DecodeStepDetails
        => _decodeStepDetails ??= [.. DecodeSteps.Select((s, i) => new DecodeStepDetail { Step = s, Ordinal = i })];

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

    public IReadOnlyList<HuffmanCodeDetail> Codes
        => _codes ??= SelectedPage?.Codes.Select(c => new HuffmanCodeDetail { Code = c }).ToList() ?? [];

    [ObservableProperty]
    private int _selectedSymbol = -1;

    partial void OnSelectedEntryChanged(DictionaryEntryDetail? value) => ClearDecodeCache();

    partial void OnBlobChanged(DictionaryBlob? value)
    {
        _codes = null;

        _handles = null;

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

        SelectedHandle = null;

        Hex.SelectedMarker = null;

        _pageLoad = ApplySelectedPageAsync(value);

        if (value is not null && !_isLoading)
        {
            Hex.GoToOffset(value.Offset);
        }
    }

    private Task? _pageLoad;

    private const int DecodeTabIndex = 1;

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

        using var timing = Logger.Time("Load page detail", $"page {page.Index}, {page.StringCount} entries");

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

        SelectMarker(entry is null ? null : GetEntryItemType());
    }

    /// <summary>
    /// What an entry is marked as, which is the value itself unless a page codes it rather than storing it
    /// </summary>
    private ItemType GetEntryItemType()
        => Blob is NumericDictionary
            ? ItemType.DictionaryValue
            : SelectedPage?.Huffman is not null
                ? ItemType.StringEntryCode
                : ItemType.StringEntryValue;

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
            _ => page.Offset + HuffmanStringPage.DataOffset + (handle.Offset / 16 * 2)
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

    private bool IsHandleSelectionShown => SelectedTabIndex == HandlesTabIndex;

    /// <summary>
    /// Whether an entry is on show, which is the entry list or a page opened on its decode
    /// </summary>
    private bool IsEntrySelectionShown => SelectedTabIndex == EntriesTabIndex
                                          || (SelectedTabIndex == PagesTabIndex
                                              && SelectedPageTabIndex == DecodeTabIndex);

    [ObservableProperty]
    private ObservableCollection<Marker> _headerMarkers = [];

    [ObservableProperty]
    private ObservableCollection<Marker> _pageMarkers = [];

    private List<Marker> BuildMarkers(DictionaryBlob blob, int start, int length)
    {
        using var timing = Logger.Time("Build markers", $"{length} bytes");

        try
        {
            var header = DictionaryMarkerBuilder.GroupHeader(
                DictionaryMarkerBuilder.Window(
                    [.. MarkerBuilder.BuildMarkers(blob), .. DictionaryMarkerBuilder.ArrayMarkers(blob)],
                    start,
                    length));

            // A selection is made on a tab and means nothing on another, so it is marked only while that tab shows
            List<Marker> selection =
            [
                .. IsEntrySelectionShown
                       ? DictionaryMarkerBuilder.EntryMarkers(Blob, SelectedPage, SelectedEntry, DecodeSteps)
                       : [],
                .. IsHandleSelectionShown
                       ? DictionaryMarkerBuilder.SelectedHandleMarkers(Blob, SelectedHandle)
                       : []
            ];

            var page = SelectedPage is { } selected
                ? DictionaryMarkerBuilder.Window([.. MarkerBuilder.BuildMarkers(selected.Page), .. selection],
                                                 start,
                                                 length)
                : DictionaryMarkerBuilder.Window(selection, start, length);

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
}
