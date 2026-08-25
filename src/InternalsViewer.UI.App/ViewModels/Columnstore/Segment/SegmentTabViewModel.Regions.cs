using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.Services.Markers;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using InternalsViewer.UI.App.Services.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Columnstore.Segment;

public sealed partial class SegmentTabViewModel
{
    private IReadOnlyList<RleRunDetail>? _rleRuns;

    private IReadOnlyList<BookmarkDetail>? _bookmarks;

    private BitpackUnitList? _bitpackUnits;

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

        using var timing = Logger.Time("Build bookmarks", $"{blob.Bookmarks.Length} bookmarks");

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
    /// The RLE array as a run of rows each entry covers, which the map draws to show the shape of the array
    /// </summary>
    public IReadOnlyList<RleRunDetail> RleRuns => _rleRuns ??= BuildRleRuns();

    private IReadOnlyList<RleRunDetail> BuildRleRuns()
    {
        if (Blob is not { } blob || !blob.Header.HasRleArray)
        {
            return [];
        }

        using var timing = Logger.Time("Build RLE runs", $"{blob.RleEntries.Length} entries");

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

    public string RleValueLabel => Blob?.Header.IsVariableLengthData == true ? "Repeat" : "Value";

    public string RleIndexLabel => Blob?.Header.IsVariableLengthData == true ? "Read" : "Bit Pack";

    public bool HasRleArray => Blob?.Header.HasRleArray ?? false;

    public bool HasBitpackArray => Blob?.Header.HasBitpackArray ?? false;

    public void SelectRun(RleRunDetail? run)
    {
        if (run is not null)
        {
            GoToTarget(new SegmentNavigationTarget(SegmentRegion.RleArray, run.Offset));
        }
    }

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

    /// <summary>
    /// Unit the bit ruler breaks apart, found from the marker selected in the bit pack region
    /// </summary>
    [ObservableProperty]
    private BitpackUnitDetail? _bitpackUnit;

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
    /// The bit ruler follows whatever the marker tree has picked, the marker now living on the hex view model
    /// </summary>
    private void OnHexPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BlobHexViewModel.SelectedMarker))
        {
            BitpackUnit = GetBitpackUnit(Hex.SelectedMarker);
        }
    }

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

            Hex.SelectedMarker = MarkerLookup.FindByType(Hex.Markers, ItemType.SegmentRowSource);
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
    /// Hands the grid an indexed view over the segment, the rows themselves being worked out on demand
    /// </summary>
    private void BuildRows(SegmentBlob blob)
    {
        using var timing = Logger.Time("Build rows");

        var stream = _dataIdStream ??= new SegmentDataIdStream(blob);

        var context = new SegmentRowContext(blob, stream, DeriveValueTimed, IsDerivationVisible);

        Rows = new SegmentRowList(context, stream.RowCount);

        RowCountDescription = stream.RowCount == 1 ? "1 row" : $"{stream.RowCount} rows";
    }

    /// <summary>
    /// Everything the window holds, being the region on show plus the row picked in the data grid
    /// </summary>
    private List<Marker> BuildMarkers(SegmentBlob blob, int start, int length)
    {
        using var timing = Logger.Time("Build markers", $"{Region}, {length} bytes");

        // The row was picked on the data tab, so its source is marked only while that tab is the one showing
        var rows = SelectedRegionTabIndex == DataTabIndex
            ? SegmentRegionMarkerBuilder.Window(RowMarkers(), start, length)
            : [];

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
}
