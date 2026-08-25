using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Columnstore.Segments;
using InternalsViewer.UI.App.Controls.HexView;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Columnstore.Segment;
using InternalsViewer.UI.App.Services.Markers;
using InternalsViewer.UI.App.ViewModels.Columnstore;

namespace InternalsViewer.UI.App.ViewModels.Columnstore.Segment;

public sealed partial class SegmentTabViewModel
{
    private const int DecodeTabIndex = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVariableWidthValues))]
    private ValuePageSummary? _selectedValuePage;

    [ObservableProperty]
    private ValueList? _values;

    [ObservableProperty]
    private ValueDetail? _selectedValue;

    /// <summary>
    /// Whether the segment holds a paged value store in place of the run length and bit pack pair
    /// </summary>
    public bool HasVariableLengthData => Blob?.VariableLengthData is not null;

    /// <summary>
    /// Whether the page on show locates its values through an offset array rather than a fixed stride
    /// </summary>
    public bool HasVariableWidthValues => SelectedValuePage?.Page.IsVariableWidth ?? false;

    public ObservableCollection<ValuePageSummary> ValuePages { get; } = [];

    /// <summary>
    /// The expanded payload of the selected page, which is where a value has a place of its own
    /// </summary>
    /// <remarks>
    /// A second window over a different run of bytes entirely - the blob hex above shows the page compressed, this
    /// shows what it decompresses to, so their offsets have nothing to do with one another.
    /// </remarks>
    public BlobHexViewModel PayloadHex { get; } = new();

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

        // The blob hex can only show the page the value was read from, the payload hex holding the value itself
        PayloadHex.SelectedMarker = MarkerLookup.FindByType(PayloadHex.Markers,
                                                            position >= 0
                                                                ? ItemType.DictionaryValue
                                                                : ItemType.ValueOffsetEntry);

        SelectPayloadMarker();
    }

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
}
