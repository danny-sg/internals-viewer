﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Engine;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Windows.UI;
using InternalsViewer.Internals.Helpers;
using AllocationUnit = InternalsViewer.Internals.Engine.Database.AllocationUnit;

namespace InternalsViewer.UI.App.ViewModels.Page;

public class PageTabViewModelFactory(ILogger<PageTabViewModel> logger, IPageService pageService, IRecordService recordService)
{
    private ILogger<PageTabViewModel> Logger { get; } = logger;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    public PageTabViewModel Create(DatabaseSource database)
    {
        return new PageTabViewModel(Logger, PageService, RecordService, database);
    }
}

public partial class PageTabViewModel(ILogger<PageTabViewModel> logger,
                                      IPageService pageService,
                                      IRecordService recordService,
                                      DatabaseSource database)
    : TabViewModel
{
    private ILogger<PageTabViewModel> Logger { get; } = logger;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    [ObservableProperty]
    private string objectName = string.Empty;

    [ObservableProperty]
    private string indexName = string.Empty;

    [ObservableProperty]
    private string objectIndexType = string.Empty;

    [ObservableProperty]
    private string indexType = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private PageAddress pageAddress;

    [ObservableProperty]
    private PageAddress nextPage;

    [ObservableProperty]
    private PageAddress previousPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PageForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(PageBackCommand))]
    private Internals.Engine.Pages.Page page = new EmptyPage();

    [ObservableProperty]
    private DatabaseSource database = database;

    [ObservableProperty]
    private ObservableCollection<OffsetSlot> offsets = new();

    [ObservableProperty]
    private OffsetSlot? selectedSlot;

    [ObservableProperty]
    private Marker? selectedMarker;

    [ObservableProperty]
    private bool includeHeaderMarkers;

    [ObservableProperty]
    private ObservableCollection<Marker> markers = new();

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> allocationLayers = new();

    [ObservableProperty]
    private string rowDataTabName = "Row Data";

    [ObservableProperty]
    private bool isRowDataTabVisible;

    [ObservableProperty]
    private bool isAllocationsTabVisible;

    [ObservableProperty]
    private int selectedTabIndex;

    private const int HeaderTab = 0;
    private const int RowDataTabIndex = 1;
    private const int AllocationsTabIndex = 2;

    private List<Record> Records { get; } = new();

    private History<PageAddress> History { get; } = new();

    partial void OnIncludeHeaderMarkersChanged(bool value)
    {
        AddPageMarkers(Page);
    }

    partial void OnSelectedSlotChanged(OffsetSlot? value)
    {
        if (value == null)
        {
            Markers.Clear();
            return;
        }

        AddRecordMarkers(value.Offset);
    }

    private void AddRecordMarkers(ushort value)
    {
        var record = Records.FirstOrDefault(r => r.SlotOffset == value);

        if (record is null)
        {
            return;
        }

        AddMarkers(record);
    }

    private void AddMarkers(DataStructure source)
    {
        var pageMarkers = GetPageMarkers(Page);

        var recordMarkers = MarkerBuilder.BuildMarkers(source);

        Markers = new ObservableCollection<Marker>(pageMarkers.Concat(recordMarkers).OrderBy(o => o.StartPosition));
    }

    [RelayCommand]
    public async Task LoadPage(PageAddress address)
    {
        Logger.LogDebug("Loading Page {Address}", address);

        IsLoading = true;

        Name = $"Loading Page {address}...";

        PageAddress = address;

        var resultPage = await PageService.GetPage(Database, address);

        Name = $"{PageHelpers.GetPageTypeShortName(resultPage.PageHeader.PageType)} Page {address}";

        History.Add(pageAddress);

        Logger.LogDebug("Building Offset Table");

        var slots = resultPage.OffsetTable.Select((s, i) => new OffsetSlot
        {
            Index = (ushort)i,
            Offset = s,
            Description = $"0x{s:X}"
        });

        SelectedSlot = null;
        SelectedMarker = null;

        Offsets = new ObservableCollection<OffsetSlot>(slots);

        Logger.LogDebug("Adding Page Markers");

        AddPageMarkers(resultPage);

        switch (resultPage)
        {
            case AllocationUnitPage allocationUnitPage:
                DisplayAllocationUnitPage(allocationUnitPage);

                break;
            case IamPage iamPage:
                DisplayIamPage(iamPage);

                break;
            case AllocationPage allocationPage:
                DisplayAllocationPage(allocationPage);

                break;

            default:
                IndexName = string.Empty;
                ObjectName = string.Empty;
                IndexType = string.Empty;
                ObjectIndexType = string.Empty;
                break;
        }

        Page = resultPage;

        NextPage = new PageAddress(pageAddress.FileId, pageAddress.PageId + 1);

        if (pageAddress.PageId > 0)
        {
            PreviousPage = new PageAddress(pageAddress.FileId, pageAddress.PageId - 1);
        }

    

        IsLoading = false;
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task PageBack()
    {
        var back = History.Back();

        if (back != default)
        {
            await LoadPage(back);
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private async Task PageForward()
    {
        var forward = History.Forward();

        if (forward != default)
        {
            await LoadPage(forward);
        }
    }

    private bool CanGoForward() => History.CanGoForward();

    private bool CanGoBack() => History.CanGoBack();

    private void DisplayIamPage(IamPage iamPage)
    {
        LoadAllocationLayer(iamPage);

        AddMarkers(iamPage);

        SetAllocationUnitDescription(iamPage.AllocationUnit);

        RowDataTabName = "IAM Header/Single Page Allocations";

        IsRowDataTabVisible = true;
        IsAllocationsTabVisible = true;

        SelectedTabIndex = SelectedTabIndex == RowDataTabIndex ? AllocationsTabIndex : SelectedTabIndex;
    }

    private void DisplayAllocationPage(AllocationPage allocationPage)
    {
        LoadAllocationLayer(allocationPage);

        IsAllocationsTabVisible = true;
        IsRowDataTabVisible = false;

        SelectedTabIndex = SelectedTabIndex == RowDataTabIndex ? AllocationsTabIndex : SelectedTabIndex;
    }

    private void DisplayAllocationUnitPage(AllocationUnitPage allocationUnitPage)
    {
        SetAllocationUnitDescription(allocationUnitPage.AllocationUnit);

        LoadRecords(allocationUnitPage);

        RowDataTabName = "Row Data";

        IsAllocationsTabVisible = false;
        IsRowDataTabVisible = true;

        SelectedSlot = Offsets.FirstOrDefault();

        SelectedTabIndex = SelectedTabIndex == AllocationsTabIndex ? RowDataTabIndex : SelectedTabIndex;
    }

    private void LoadAllocationLayer(AllocationPage allocationPage)
    {
        var layer = AllocationLayerBuilder.GenerateLayer(allocationPage);

        layer.Name = $"Allocation Page {allocationPage.PageAddress}";
        layer.Colour = System.Drawing.Color.Brown;
        layer.IsVisible = true;

        AllocationLayers = new ObservableCollection<AllocationLayer>(new[] { layer });
    }

    private void SetAllocationUnitDescription(AllocationUnit allocationUnit)
    {
        ObjectName = $"{allocationUnit.SchemaName}.{allocationUnit.TableName}";

        IndexName = allocationUnit.IndexName;
        IndexType = allocationUnit.IndexType == Internals.Engine.Database.Enums.IndexType.NonClustered
                                                         ? "Non-Clustered"
                                                         : string.Empty;
        ObjectIndexType = allocationUnit.ParentIndexType == Internals.Engine.Database.Enums.IndexType.Clustered
                                                         ? "Clustered"
                                                         : "Heap";
    }

    private void LoadRecords(Internals.Engine.Pages.Page target)
    {
        Logger.LogDebug("Loading Records");

        Records.Clear();

        foreach (var slot in Offsets)
        {
            try
            {
                switch (target.PageHeader.PageType)
                {
                    case PageType.Data:
                        Logger.LogDebug("Loading Data Records");

                        Records.Add(RecordService.GetDataRecord((DataPage)target, slot.Offset));
                        break;
                    case PageType.Index:
                        Logger.LogDebug("Loading Index Records");

                        Records.Add(RecordService.GetIndexRecord((IndexPage)target, slot.Offset));
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error loading record {slot.Index}");
            }
        }

        Logger.LogDebug("{RecordCount} Record(s) loaded", Records.Count);
    }

    /// <summary>
    /// Add the header and offset table markers (applies to all pages)
    /// </summary>
    private void AddPageMarkers(Internals.Engine.Pages.Page p)
    {
        var m = GetPageMarkers(p);

        Markers = new ObservableCollection<Marker>(m);
    }

    private List<Marker> GetPageMarkers(Internals.Engine.Pages.Page p)
    {
        var m = new List<Marker>();

        m.Add(new Marker
        {
            Name = "Page Header",
            StartPosition = 0,
            EndPosition = 95,
            ForeColour = Colors.Blue,
            BackColour = Color.FromArgb(1, 245, 245, 250),
            IsVisible = false
        });

        if (IncludeHeaderMarkers)
        {
            var headerMarkers = MarkerBuilder.BuildMarkers(p.PageHeader);

            m.AddRange(headerMarkers);
        }

        var offsetTableStart = PageData.Size - p.PageHeader.SlotCount * 2;

        m.Add(new Marker
        {
            Name = "Offset Table",
            StartPosition = offsetTableStart,
            EndPosition = PageData.Size,
            ForeColour = Colors.Green,
            BackColour = Color.FromArgb(1, 245, 250, 245),
            IsVisible = false
        });

        return m;
    }
}
