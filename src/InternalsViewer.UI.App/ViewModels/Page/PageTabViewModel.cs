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
using InternalsViewer.Query.TransactionLog;
using InternalsViewer.Query.TransactionLog.LogRecords;
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
    [NotifyPropertyChangedFor(nameof(LogRecordsVisibility))]
    [NotifyPropertyChangedFor(nameof(LogRecordsHeight))]
    private ObservableCollection<LogRecordItem> _logRecords = [];

    [ObservableProperty]
    private PageSlot? _selectedSlot;

    [ObservableProperty]
    private LogRecordItem? _selectedLogRecord;

    [ObservableProperty]
    private ObservableCollection<LogRecordAnnotation> _changeSpans = [];

    [ObservableProperty]
    private LogRecordAnnotation? _selectedChangeSpan;

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

    [ObservableProperty]
    private string _replayStatus = string.Empty;

    private const int HeaderTab = 0;
    private const int RowDataTabIndex = 1;
    private const int AllocationsTabIndex = 2;

    private const short PageHeaderSlot = -100;
    private const short IamHeaderSlot = -10;
    private const short CompressionInfoSlot = -90;

    public Visibility LogRecordsVisibility => LogRecords.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public GridLength LogRecordsHeight => LogRecords.Count > 0
        ? new GridLength(1, GridUnitType.Star)
        : new GridLength(0);

    private List<IRecord> Records { get; } = [];

    private History<PageAddress> History { get; } = new();

    private byte[]? _baselineData;

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
        DispatcherQueue.TryEnqueue(() =>
        {
            IsLoading = true;

            Name = $"Loading Page {pageAddress}...";

            PageAddress = pageAddress;
        });

        await Task.Run(async () =>
            {
                var resultPage = await PageService.GetPage(Database, pageAddress, CancellationToken);

                _baselineData = (byte[])resultPage.Data.Clone();

                DispatcherQueue.TryEnqueue(() =>
                {
                    Name = $"{PageHelpers.GetPageTypeShortName(resultPage.PageHeader.PageType)} " +
                           $"Page {pageAddress}";

                    DisplayPage(resultPage, slot);

                    NextPage = new PageAddress(PageAddress.FileId, PageAddress.PageId + 1);

                    if (PageAddress.PageId > 0)
                    {
                        PreviousPage = new PageAddress(PageAddress.FileId, PageAddress.PageId - 1);
                    }

                    ReplayStatus = string.Empty;

                    ChangeSpans = [];
                    SelectedChangeSpan = null;

                    IsLoading = false;
                });
            }, CancellationToken);

        History.Add(PageAddress);
    }

    private void DisplayPage(Internals.Engine.Pages.Page resultPage, ushort? slot)
    {
        var headerSlot = new PageSlot
        {
            Index = PageHeaderSlot,
            Description = "Page Header"
        };

        var slots = resultPage.OffsetTable.Select((s, i) => new PageSlot
        {
            Index = (short)i,
            Offset = s,
            Description = $"0x{s:X}"
        }).ToList();

        slots.Insert(0, headerSlot);

        var selectedSlot = slots.FirstOrDefault(s => s.Index == slot);

        Logger.LogDebug("Building Offset Table");

        switch (resultPage)
        {
            case FileHeaderPage:
                break;
            case AllocationUnitPage allocationUnitPage:
                DisplayAllocationUnitPage(allocationUnitPage);

                if (allocationUnitPage.CompressionInfo != null)
                {
                    slots.Insert(1, new PageSlot
                    {
                        Index = CompressionInfoSlot,
                        Description = "Compression Info"
                    });
                }
                break;
            case IamPage iamPage:
                DisplayIamPage(iamPage);

                slots.Insert(1, new PageSlot
                {
                    Index = IamHeaderSlot,
                    Description = "IAM Header"
                });

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

        AddPageMarkers(resultPage);
        AddPageHeaderMarkers();
    }

    partial void OnSelectedLogRecordChanged(LogRecordItem? value)
    {
        _ = ShowLogRecordState(value);
    }

    partial void OnSelectedChangeSpanChanged(LogRecordAnnotation? value)
    {
        if (value is null)
        {
            return;
        }

        var slot = FindSlotForOffset(value.Offset);

        if (slot is not null)
        {
            SelectedSlot = slot;
        }
    }

    /// <summary>
    /// Finds the slot associated with a page offset
    /// </summary>
    /// <remarks>
    /// Offsets in the 96 byte page header select the header pseudo-slot; offsets in the offset table at the end of
    /// the page select the slot the entry belongs to; anything else selects the slot whose row starts closest
    /// before the offset
    /// </remarks>
    private PageSlot? FindSlotForOffset(int offset)
    {
        if (offset < 96)
        {
            return PageSlots.FirstOrDefault(s => s.Index == PageHeaderSlot);
        }

        var offsetTableStart = PageData.Size - Page.PageHeader.SlotCount * 2;

        if (offset >= offsetTableStart)
        {
            var slotId = (PageData.Size - 1 - offset) / 2;

            return PageSlots.FirstOrDefault(s => s.Index == slotId);
        }

        return PageSlots.Where(s => s.Index >= 0 && s.Offset > 0 && s.Offset <= offset)
                        .OrderByDescending(s => s.Offset)
                        .FirstOrDefault();
    }

    private async Task ShowLogRecordState(LogRecordItem? item)
    {
        if (_baselineData is null || IsLoading)
        {
            return;
        }

        var baseline = (byte[])_baselineData.Clone();

        var pageItems = LogRecords.Where(i => i.Record.PageAddress == PageAddress)
                                  .OrderBy(i => (i.Record.Lsn.VirtualLogFile,
                                                 i.Record.Lsn.FileOffset,
                                                 i.Record.Lsn.RecordSequence))
                                  .ToList();

        var target = item is not null && item.Record.PageAddress == PageAddress ? item : null;

        try
        {
            await Task.Run(() =>
            {
                var currentSlot = (ushort?)SelectedSlot?.Index;

                var page = PageService.ParsePage(Database, PageAddress, baseline);

                var status = string.Empty;

                var annotations = new Dictionary<LogRecordItem, List<LogRecordAnnotation>>();

                if (target is not null && pageItems.Count > 0)
                {
                    LogRecordApplier.Rebase(page, pageItems[0].Record.PreviousPageLsn);

                    var targetLsn = (target.Record.Lsn.VirtualLogFile,
                                     target.Record.Lsn.FileOffset,
                                     target.Record.Lsn.RecordSequence);

                    foreach (var pageItem in pageItems)
                    {
                        if ((pageItem.Record.Lsn.VirtualLogFile,
                             pageItem.Record.Lsn.FileOffset,
                             pageItem.Record.Lsn.RecordSequence).CompareTo(targetLsn) > 0)
                        {
                            break;
                        }

                        var result = LogRecordApplier.Apply(page, pageItem.Record);

                        if (!result.IsApplied)
                        {
                            status = $"Replay stopped at {pageItem.Record.Lsn.ToBinaryString()} " +
                                     $"({result.Status}): {result.Message}";
                            break;
                        }

                        annotations[pageItem] = result.Changes
                                                      .Select(c => new LogRecordAnnotation
                                                      {
                                                          Offset = c.Offset,
                                                          Length = c.Length,
                                                          Description = c.Description
                                                      })
                                                      .ToList();
                    }

                    page = PageService.ParsePage(Database, PageAddress, page.Data);

                    if (status.Length == 0)
                    {
                        status = $"Page state as of {target.Record.Lsn.ToBinaryString()}";
                    }
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    SelectedChangeSpan = null;

                    foreach (var pageItem in pageItems)
                    {
                        pageItem.Annotations = new ObservableCollection<LogRecordAnnotation>(
                            annotations.GetValueOrDefault(pageItem, []));
                    }

                    ChangeSpans = new ObservableCollection<LogRecordAnnotation>(
                        target is not null ? annotations.GetValueOrDefault(target, []) : []);

                    // Reassigned so the log record tree rebuilds with the new annotation children
                    LogRecords = new ObservableCollection<LogRecordItem>(LogRecords);

                    DisplayPage(page, currentSlot);

                    ReplayStatus = status;
                });
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error replaying log records for page {PageAddress}", PageAddress);

            ReplayStatus = $"Replay failed: {ex.Message}";
        }
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
            Records.AddRange(RecordService.GetRecords(target));
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
