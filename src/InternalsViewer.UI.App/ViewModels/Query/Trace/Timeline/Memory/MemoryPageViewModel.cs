using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Query.FullTrace.Memory.History.Pages;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Page;
using InternalsViewer.UI.App.ViewModels.Page;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryPageViewModel : ObservableObject
{
    public const int RowDataTab = 0;

    public const int DataTab = 1;

    private const int LoadInterval = 50;

    private const int PageTypeOffset = 1;

    private const string WorkfileTitle = "Workfile Page";

    private const string SortTitle = "Sort Page";

    private const string RowDataName = "Row Data";

    private const string NotAllocated = "Not Allocated At This Position";

    private const string NotPage = "Not A Page At This Position";

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _title = WorkfileTitle;

    [ObservableProperty]
    private ObservableCollection<PageSlot> _slots = [];

    [ObservableProperty]
    private PageSlot? _selectedSlot;

    [ObservableProperty]
    private ObservableCollection<Marker> _markers = [];

    [ObservableProperty]
    private Marker? _selectedMarker;

    [ObservableProperty]
    private string _markerTabName = RowDataName;

    [ObservableProperty]
    private QueryResultSet? _records;

    [ObservableProperty]
    private ResultRow<long>? _selectedRecordRow;

    [ObservableProperty]
    private bool _isDataTabVisible;

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    public MemoryPageViewModel()
    {
        Loader = new PlayheadLoader<LoadedPage>(TimeSpan.FromMilliseconds(LoadInterval), ApplyPage, b => IsLoading = b);
    }

    private PlayheadMemoryViewModel? Owner { get; set; }

    private PlayheadLoader<LoadedPage> Loader { get; }

    private RowRecordLayout? Layout { get; set; }

    private SortRecordLayout? SortLayout { get; set; }

    private WorkfilePage? Page { get; set; }

    private byte[]? Data { get; set; }

    private byte[]? LoadedData { get; set; }

    public void Load(PlayheadMemoryViewModel owner)
    {
        Clear();

        Owner = owner;

        IsAvailable = owner.History.IsPage(owner.Block);

        Layout = IsAvailable ? owner.History.GetWorkfileLayout(owner.Block) : null;

        SortLayout = IsAvailable ? owner.History.GetSortLayout(owner.Block) : null;
    }

    public void SetData(byte[]? data)
    {
        if (!IsAvailable || ReferenceEquals(data, Data))
        {
            return;
        }

        Data = data;

        Title = data is { Length: > PageTypeOffset } && data[PageTypeOffset] == (byte)PageType.Sort ? SortTitle : WorkfileTitle;
    }

    public void Refresh()
    {
        if (!IsAvailable || (LoadedData is not null && ReferenceEquals(LoadedData, Data)))
        {
            return;
        }

        if (Data is not { } data)
        {
            Loader.Cancel();

            ApplyPage(new LoadedPage(null, null));

            EmptyText = NotAllocated;

            return;
        }

        var layout = Layout;

        var sortLayout = SortLayout;

        Loader.Request(() => new LoadedPage(data, WorkfilePageReader.Read(data, layout, sortLayout)));
    }

    public void Clear()
    {
        Loader.Cancel();

        Owner = null;

        IsAvailable = false;

        Layout = null;

        SortLayout = null;

        Page = null;

        Data = null;

        LoadedData = null;

        Slots = [];

        SelectedSlot = null;

        Markers = [];

        MarkerTabName = RowDataName;

        Records = null;

        IsDataTabVisible = false;

        SelectedTab = RowDataTab;

        EmptyText = string.Empty;

        Title = WorkfileTitle;
    }

    partial void OnSelectedSlotChanged(PageSlot? value) => ShowSlot(value);

    partial void OnSelectedRecordRowChanged(ResultRow<long>? value)
    {
        if (value is { Id: var slot } && Slots.FirstOrDefault(s => s.Index == slot) is { } selected && selected != SelectedSlot)
        {
            SelectedSlot = selected;
        }
    }

    private void ApplyPage(LoadedPage loaded)
    {
        LoadedData = loaded.Data;

        Page = loaded.Page;

        var selected = SelectedSlot?.Index;

        EmptyText = loaded.Page is null ? NotPage : string.Empty;

        Records = loaded.Page is { HasValues: true } page ? RecordResultSetHelper.ToResultSet([.. page.Records]) : null;

        IsDataTabVisible = Records is not null;

        if (!IsDataTabVisible && SelectedTab == DataTab)
        {
            SelectedTab = RowDataTab;
        }

        Slots = new ObservableCollection<PageSlot>(loaded.Page is null ? [] : PageDisplayBuilder.GetSlots(loaded.Page.Page));

        SelectedSlot = Slots.FirstOrDefault(s => s.Index == selected);

        ShowSlot(SelectedSlot);
    }

    private void ShowSlot(PageSlot? slot)
    {
        if (Page is not { } page || slot is null)
        {
            MarkerTabName = RowDataName;

            Markers = [];

            Owner?.ShowPageMarkers(null);

            return;
        }

        int offset;

        switch (slot.Index)
        {
            case PageDisplayBuilder.PageHeaderSlot:
                MarkerTabName = "Page Header";

                Markers = new ObservableCollection<Marker>(PageMarkers.GetHeaderMarkers(page.Page));

                offset = 0;
                break;

            case PageDisplayBuilder.OffsetTableSlot:
                MarkerTabName = "Slot Offset Table";

                var (markers, start) = PageMarkers.GetOffsetTableMarkers(page.Page);

                Markers = new ObservableCollection<Marker>(markers);

                offset = start;
                break;

            default:
                MarkerTabName = $"Slot {slot.Description}";

                Markers = page.Records.FirstOrDefault(r => r.Offset == slot.Offset) is { } record
                    ? new ObservableCollection<Marker>(PageMarkers.GetRecordMarkers(page.Page, record))
                    : [];

                offset = slot.Offset;
                break;
        }

        if (Records?.Rows.FirstOrDefault(r => r.Id == slot.Index) is { } row && row != SelectedRecordRow)
        {
            SelectedRecordRow = row;
        }

        Owner?.ShowPageMarkers(offset);
    }

    private sealed record LoadedPage(byte[]? Data, WorkfilePage? Page);
}
