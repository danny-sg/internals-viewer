using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Annotations;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services.Markers;
using InternalsViewer.UI.App.ViewModels.Allocation;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using AllocationUnit = InternalsViewer.Internals.Engine.Database.AllocationUnit;

namespace InternalsViewer.UI.App.ViewModels.Page;

public sealed class PageTabViewModelFactory(ILogger<PageTabViewModel> logger,
                                            IPageService pageService,
                                            IRecordService recordService)
{
    private ILogger<PageTabViewModel> Logger { get; } = logger;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    public PageTabViewModel Create(DatabaseSource database)
    {
        return new PageTabViewModel(Logger, PageService, RecordService, database);
    }
}

public sealed partial class PageTabViewModel(ILogger<PageTabViewModel> logger,
                                             IPageService pageService,
                                             IRecordService recordService,
                                             DatabaseSource database)
    : TabViewModel
{
    private ILogger<PageTabViewModel> Logger { get; } = logger;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    [ObservableProperty]
    private string _objectName = string.Empty;

    [ObservableProperty]
    private int _objectId;

    [ObservableProperty]
    private int _indexId;

    [ObservableProperty]
    private string _indexName = string.Empty;

    [ObservableProperty]
    private string _objectIndexType = string.Empty;

    [ObservableProperty]
    private string _indexType = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private PageAddress _pageAddress;

    [ObservableProperty]
    private PageAddress _nextPage;

    [ObservableProperty]
    private PageAddress _previousPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PageForwardCommand))]
    [NotifyCanExecuteChangedFor(nameof(PageBackCommand))]
    private Internals.Engine.Pages.Page _page = new EmptyPage();

    [ObservableProperty]
    private DatabaseSource _database = database;

    [ObservableProperty]
    private ObservableCollection<PageSlot> _pageSlots = [];

    [ObservableProperty]
    private PageSlot? _selectedSlot;

    [ObservableProperty]
    private Marker? _selectedMarker;

    [ObservableProperty]
    private ObservableCollection<Marker> _markers = [];

    [ObservableProperty]
    private ObservableCollection<AllocationLayer> _allocationLayers = [];

    [ObservableProperty]
    private string _markerTabName = "Page Header";

    [ObservableProperty]
    private bool _isRowDataTabVisible;

    [ObservableProperty]
    private bool _isAllocationsTabVisible;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private short _allocationFileId;

    private const int HeaderTab = 0;
    private const int RowDataTabIndex = 1;
    private const int AllocationsTabIndex = 2;

    private const short PageHeaderSlot = -100;
    private const short IamHeaderSlot = -10;
    private const short CompressionInfoSlot = -90;

    private List<IRecord> Records { get; } = [];

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
    public async Task LoadPage(PageAddress pageAddress)
    {
        await LoadPage(pageAddress, null);
    }

    [RelayCommand]
    public async Task LoadRowIdentifier(RowIdentifier rowIdentifier)
    {
        await LoadPage(rowIdentifier.PageAddress, rowIdentifier.SlotId);
    }

    public async Task LoadPage(PageAddress pageAddress, ushort? slot)
    {
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

        DispatcherQueue.TryEnqueue(() =>
        {
            IsLoading = true;

            Name = $"Loading Page {pageAddress}...";

            PageAddress = pageAddress;
        });

        await Task.Run(async () =>
            {
                var resultPage = await PageService.GetPage(Database, pageAddress);

                var slots = resultPage.OffsetTable.Select((s, i) => new PageSlot
                {
                    Index = (short)i,
                    Offset = s,
                    Description = $"0x{s:X}"
                }).ToList();

                slots.Insert(0, headerSlot);

                var selectedSlot = slots.FirstOrDefault(s => s.Index == slot);

                DispatcherQueue.TryEnqueue(() =>
                {
                    Name = $"{PageHelpers.GetPageTypeShortName(resultPage.PageHeader.PageType)} " +
                           $"Page {pageAddress}";

                    Logger.LogDebug("Building Offset Table");

                    switch (resultPage)
                    {
                        case FileHeaderPage fileHeaderPage:
                            break;
                        case AllocationUnitPage allocationUnitPage:
                            DisplayAllocationUnitPage(allocationUnitPage);

                            if (allocationUnitPage.CompressionInfo != null)
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

                    SelectedSlot = selectedSlot ?? headerSlot;
                    SelectedMarker = null;

                    Page = resultPage;

                    NextPage = new PageAddress(PageAddress.FileId, PageAddress.PageId + 1);

                    if (PageAddress.PageId > 0)
                    {
                        PreviousPage = new PageAddress(PageAddress.FileId, PageAddress.PageId - 1);
                    }

                    AddPageMarkers(resultPage);
                    AddPageHeaderMarkers();

                    IsLoading = false;
                });
            });

        History.Add(PageAddress);
    }


    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task PageBack()
    {
        var back = History.Back();

        if (back != default)
        {
            await LoadPage(back, null);
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private async Task PageForward()
    {
        var forward = History.Forward();

        if (forward != default)
        {
            await LoadPage(forward, null);
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadPage(PageAddress, null);
    }

    [RelayCommand(CanExecute = nameof(CanOpenIndexView))]
    private async Task OpenIndexView()
    {
        if (Page is AllocationUnitPage allocationUnitPage)
        {
            var rootPage = allocationUnitPage.AllocationUnit.RootPage;

            await WeakReferenceMessenger.Default.Send(new OpenIndexMessage(new OpenIndexRequest(Database, rootPage)));
        }
    }

    private bool CanOpenIndexView() => Page is AllocationUnitPage allocationUnitPage
                                       && allocationUnitPage.AllocationUnit.IndexType
                                            != Internals.Engine.Database.Enums.IndexType.Heap;

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
        var layer = AllocationLayerBuilder.GenerateLayer(iamPage, iamPage.StartPage.PageId);

        // IAMs are not necessarily in the same file as where they are tracking. The Start Page file determines the file
        AllocationFileId = iamPage.StartPage.FileId;

        layer.Name = $"IAM Page {iamPage.PageAddress}";
        layer.Colour = System.Drawing.Color.Brown;

        layer.IsVisible = true;

        AllocationLayers = new ObservableCollection<AllocationLayer>(new[] { layer });
    }

    private void LoadAllocationLayer(AllocationPage allocationPage)
    {
        var layer = AllocationLayerBuilder.GenerateLayer(allocationPage, 0);

        AllocationFileId = allocationPage.PageAddress.FileId;

        layer.Name = $"Allocation Page {allocationPage.PageAddress}";
        layer.Colour = System.Drawing.Color.Brown;

        layer.IsVisible = true;

        AllocationLayers = new ObservableCollection<AllocationLayer>(new[] { layer });
    }

    private void SetAllocationUnitDescription(AllocationUnit allocationUnit)
    {
        ObjectName = $"{allocationUnit.SchemaName}.{allocationUnit.TableName}";
        ObjectId = allocationUnit.ObjectId;

        IndexName = allocationUnit.IndexName;
        IndexId = allocationUnit.IndexId;

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
            Records.AddRange(RecordService.GetRecords(target, isMarkEnabled: true));
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

        var record = Records.FirstOrDefault(r => r.Offset == pageSlot.Offset);

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

    private void AddMarkers(IDataStructure source)
    {
        var pageMarkers = GetPageMarkers(Page);

        var recordMarkers = MarkerBuilder.BuildMarkers(source);

        Markers = new ObservableCollection<Marker>(pageMarkers.Concat(recordMarkers).OrderBy(o => o.StartPosition));
    }

    private static List<Marker> GetPageMarkers(PageData p)
    {
        var m = new List<Marker>
        {
            new()
            {
                Name = "Page Header",
                StartPosition = 0,
                EndPosition = 95,
                ForeColour = Colors.Blue,
                BackColour = Color.FromArgb(1, 245, 245, 250),
                IsVisible = false
            }
        };

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
