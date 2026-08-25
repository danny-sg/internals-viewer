using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Dictionaries;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore.Dictionary;
using InternalsViewer.UI.App.Services.Markers;

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
    }

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

    [ObservableProperty]
    private ObservableCollection<Marker> _headerMarkers = [];

    [ObservableProperty]
    private ObservableCollection<Marker> _pageMarkers = [];

    private List<Marker> BuildMarkers(DictionaryBlob blob, int start, int length)
    {
        try
        {
            var header = DictionaryMarkerBuilder.GroupHeader(
                DictionaryMarkerBuilder.Window(
                    [.. MarkerBuilder.BuildMarkers(blob), .. DictionaryMarkerBuilder.ArrayMarkers(blob)],
                    start,
                    length));

            List<Marker> selection =
            [
                .. DictionaryMarkerBuilder.EntryMarkers(Blob, SelectedPage, SelectedEntry, DecodeSteps),
                .. DictionaryMarkerBuilder.SelectedHandleMarkers(Blob, SelectedHandle)
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
