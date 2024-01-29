using System;
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
using InternalsViewer.Internals.Compression;
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
    private ObservableCollection<PageSlot> pageSlots = new();

    [ObservableProperty]
    private PageSlot? selectedSlot;

    [ObservableProperty]
    private Marker? selectedMarker;

    [ObservableProperty]
    private ObservableCollection<Marker> markers = new();

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> allocationLayers = new();

    [ObservableProperty]
    private string markerTabName = "Page Header";

    [ObservableProperty]
    private bool isRowDataTabVisible;

    [ObservableProperty]
    private bool isAllocationsTabVisible;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private short allocationFileId;

    private const int HeaderTab = 0;
    private const int RowDataTabIndex = 1;
    private const int AllocationsTabIndex = 2;

    private const short PageHeaderSlot = -100;
    private const short IamHeaderSlot = -10;
    private const short CompressionInfoSlot = -90;

    private List<Record> Records { get; } = new();

    private History<PageAddress> History { get; } = new();

    partial void OnSelectedSlotChanged(PageSlot? value)
    {
        if (value == null)
        {
            Markers.Clear();
            return;
        }

        switch (value.Index)
        {
            case PageHeaderSlot:
                AddPageHeaderMarkers();
                break;
            case CompressionInfoSlot:
                AddCompressionInfoMarkers();
                break;
            case IamHeaderSlot:
                AddIamHeaderMarkers();
                break;
            default:
                AddRecordMarkers(value);
                break;
        }
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

        var slots = resultPage.OffsetTable.Select((s, i) => new PageSlot
        {
            Index = (short)i,
            Offset = s,
            Description = $"0x{s:X}"
        }).ToList();

        var headerSlot = new PageSlot
        {
            Index = PageHeaderSlot,
            Description = "Page Header"
        };

        var iamHeaderSlot = new PageSlot
        {
            Index = IamHeaderSlot,
            Description = "IAM Header"
        };

        var compressionInfoSlot = new PageSlot
        {
            Index = CompressionInfoSlot,
            Description = "Compression Info"
        };  

        slots.Insert(0, headerSlot);

        switch (resultPage)
        {
            case AllocationUnitPage allocationUnitPage:
                DisplayAllocationUnitPage(allocationUnitPage);

                if(allocationUnitPage.CompressionInfo != null)
                {
                    slots.Insert(1, compressionInfoSlot);
                }
                break;
            case IamPage iamPage:
                DisplayIamPage(iamPage);

                slots.Insert(1, iamHeaderSlot);

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

        PageSlots = new ObservableCollection<PageSlot>(new[] { headerSlot }.Union(slots));

        SelectedSlot = headerSlot;
        SelectedMarker = null;

        Page = resultPage;

        NextPage = new PageAddress(pageAddress.FileId, pageAddress.PageId + 1);

        if (pageAddress.PageId > 0)
        {
            PreviousPage = new PageAddress(pageAddress.FileId, pageAddress.PageId - 1);
        }

        AddPageMarkers(resultPage);
        AddPageHeaderMarkers();

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

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadPage(PageAddress);
    }

    private bool CanGoForward() => History.CanGoForward();

    private bool CanGoBack() => History.CanGoBack();

    private void DisplayIamPage(IamPage iamPage)
    {
        LoadIamLayer(iamPage);

        SetAllocationUnitDescription(iamPage.AllocationUnit);

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

        IsAllocationsTabVisible = false;
        IsRowDataTabVisible = true;

        SelectedSlot = PageSlots.FirstOrDefault();

        SelectedTabIndex = SelectedTabIndex == AllocationsTabIndex ? RowDataTabIndex : SelectedTabIndex;
    }

    private void LoadIamLayer(IamPage iamPage)
    {
        var layer = AllocationLayerBuilder.GenerateLayer(iamPage);

        // IAMs are not necessarily in the same file as where they are tracking. The Start Page file determines the file
        AllocationFileId = iamPage.StartPage.FileId;

        layer.Name = $"IAM Page {iamPage.PageAddress}";
        layer.Colour = System.Drawing.Color.Brown;

        layer.IsVisible = true;

        AllocationLayers = new ObservableCollection<AllocationLayer>(new[] { layer });
    }

    private void LoadAllocationLayer(AllocationPage allocationPage)
    {
        var layer = AllocationLayerBuilder.GenerateLayer(allocationPage);

        AllocationFileId = allocationPage.PageAddress.FileId;

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

    private void LoadRecords(AllocationUnitPage target)
    {
        Logger.LogDebug("Loading Records");

        Records.Clear();

        try
        {
            switch (target)
            {
                case { PageHeader.PageType: PageType.Data } and { AllocationUnit.CompressionType: CompressionType.None }:

                    Records.AddRange(RecordService.GetDataRecords((DataPage)target));
                    break;

                case { PageHeader.PageType: PageType.Data } and not { AllocationUnit.CompressionType: CompressionType.None }:

                    Records.AddRange(RecordService.GetCompressedDataRecords((DataPage)target));
                    break;

                case { PageHeader.PageType: PageType.Index }:
                    Logger.LogDebug("Loading Index Records");

                    Records.AddRange(RecordService.GetIndexRecords((IndexPage)target));
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error loading record(s)");
        }

        Logger.LogDebug("{RecordCount} Record(s) loaded", Records.Count);
    }

    /// <summary>
    /// Add the header and offset table markers (applies to all pages)
    /// </summary>
    private void AddPageMarkers(PageData p)
    {
        var m = GetPageMarkers(p);

        Markers = new ObservableCollection<Marker>(m);
    }

    private void AddPageHeaderMarkers()
    {
        MarkerTabName = "Page Header";
        var headerMarkers = MarkerBuilder.BuildMarkers(Page.PageHeader);

        headerMarkers.Add(new Marker
        {
            Name = "Unused",
            StartPosition = 64,
            EndPosition = 95,
            ForeColour = Colors.Gray,
            BackColour = Colors.AliceBlue,
            IsVisible = false
        });

        Markers = new ObservableCollection<Marker>(headerMarkers);
    }

    private void AddIamHeaderMarkers()
    {
        MarkerTabName = "IAM Header";

        var m = MarkerBuilder.BuildMarkers(Page);

        Markers = new ObservableCollection<Marker>(m);
    }

    private void AddRecordMarkers(PageSlot pageSlot)
    {
        MarkerTabName = $"Slot {pageSlot.Description}";

        var record = Records.FirstOrDefault(r => r.SlotOffset == pageSlot.Offset);

        if (record is null)
        {
            return;
        }

        AddMarkers(record);
    }


    private void AddCompressionInfoMarkers()
    {
        MarkerTabName = $"Compression Info";

        if (Page is AllocationUnitPage { CompressionInfo: not null } p)
        {
            var m = MarkerBuilder.BuildMarkers(p.CompressionInfo);

            Markers = new ObservableCollection<Marker>(m);
        }
    }

    private void AddMarkers(DataStructure source)
    {
        var pageMarkers = GetPageMarkers(Page);

        var recordMarkers = MarkerBuilder.BuildMarkers(source);

        Markers = new ObservableCollection<Marker>(pageMarkers.Concat(recordMarkers).OrderBy(o => o.StartPosition));
    }

    private List<Marker> GetPageMarkers(PageData p)
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
