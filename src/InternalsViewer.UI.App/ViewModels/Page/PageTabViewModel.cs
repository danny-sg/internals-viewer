using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Interfaces.Annotations;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Services.Pages.Parsers;
using InternalsViewer.Query.Results;
using InternalsViewer.TransactionLog;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services.Markers;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
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
    private AllocationUnit? _allocationUnit;

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
    [NotifyPropertyChangedFor(nameof(IsDataTabVisible))]
    private Internals.Engine.Pages.Page _page = new EmptyPage();

    [ObservableProperty]
    private DatabaseSource _database = database;

    [ObservableProperty]
    private ObservableCollection<PageSlot> _pageSlots = [];

    [ObservableProperty]
    private ObservableCollection<LogRecordItem> _logRecords = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogRecordsVisibility))]
    [NotifyPropertyChangedFor(nameof(LogRecordsHeight))]
    private bool _hasLogRecords;

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
    private PfsChain? _pfsChain;

    [ObservableProperty]
    private IReadOnlyList<AllocationBorder>? _pfsBorders;

    [ObservableProperty]
    private string _markerTabName = "Page Header";

    [ObservableProperty]
    private bool _isRowDataTabVisible;

    [ObservableProperty]
    private bool _isAllocationsTabVisible;

    public bool IsDataTabVisible => Page.PageHeader.PageType is PageType.Data or PageType.Index;

    [ObservableProperty]
    private bool _isPfsTabVisible;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private short _allocationFileId;

    [ObservableProperty]
    private int _allocationStartPage;

    [ObservableProperty]
    private string _replayStatus = string.Empty;

    [ObservableProperty]
    private int? _scrollToOffset;

    [ObservableProperty]
    private QueryResultSet _recordsResultSet = new();

    [ObservableProperty]
    private ResultRow<long>? _selectedRecordRow;

    private const int HeaderTab = 0;

    //private const short PageHeaderSlot = PageDisplayBuilder.PageHeaderSlot;
    //private const short IamHeaderSlot = PageDisplayBuilder.IamHeaderSlot;
    //private const short CompressionInfoSlot = PageDisplayBuilder.CompressionInfoSlot;

    private PageDisplayBuilder DisplayBuilder { get; } = new(logger, recordService);

    public Visibility LogRecordsVisibility => HasLogRecords ? Visibility.Visible : Visibility.Collapsed;

    public GridLength LogRecordsHeight => HasLogRecords
        ? new GridLength(1, GridUnitType.Star)
        : new GridLength(0);

    partial void OnLogRecordsChanged(ObservableCollection<LogRecordItem> value)
    {
        UpdateLogRecordsVisibility();
    }

    partial void OnSelectedRecordRowChanged(ResultRow<long>? value)
    {
        if (SelectedSlot?.Index != value?.Id)
        {
            SelectedSlot = PageSlots.FirstOrDefault(s => s.Index == value?.Id);
        }
    }

    partial void OnPageAddressChanged(PageAddress value)
    {
        if (SelectedLogRecord is not null && SelectedLogRecord.Record.PageAddress != value)
        {
            SelectedLogRecord = null;
        }

        UpdateLogRecordsVisibility();
    }

    private void UpdateLogRecordsVisibility()
    {
        HasLogRecords = LogRecords.Any(item => item.Record.PageAddress == PageAddress);
    }

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
            case PageDisplayBuilder.PageHeaderSlot:
                AddPageHeaderMarkers();
                break;
            case PageDisplayBuilder.CompressionInfoSlot:
                AddCompressionInfoMarkers();
                break;
            case PageDisplayBuilder.IamHeaderSlot:
                AddPageMarkers(" Header");
                break;
            case PageDisplayBuilder.BootPageSlot:
                AddPageMarkers(string.Empty);
                break;
            case PageDisplayBuilder.FileHeaderSlot:
                AddPageMarkers(string.Empty);
                break;
            default:
                AddRecordMarkers(value);
                break;
        }

        ScrollToOffset = value.Offset;

        var selectedRecord = RecordsResultSet.Rows
                                             .FirstOrDefault(r => r.Id == value.Index);

        if (selectedRecord is not null && selectedRecord != SelectedRecordRow)
        {
            SelectedRecordRow = selectedRecord;
        }
    }

    [RelayCommand]
    public async Task LoadPage(PageAddress pageAddress)
    {
        await LoadPage(pageAddress, null);
    }

    [RelayCommand]
    public void SelectPfsPage(PageAddress pageAddress)
    {
        var offset = PfsPageParser.PfsOffset + pageAddress.PageId;

        Marker marker = new()
        {
            Name = $"{pageAddress} PFS Byte",
            StartPosition = offset,
            EndPosition = offset,
            ForeColour = Colors.Blue,
            BackColour = Color.FromArgb(1, 245, 245, 250),
            IsVisible = true
        };

        Markers = new ObservableCollection<Marker>([marker]);
        SelectedMarker = marker;
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

                var display = DisplayBuilder.Build(resultPage, (short?)slot, pageAddress);

                DispatcherQueue.TryEnqueue(() =>
                {
                    Name = $"{PageHelpers.GetPageTypeShortName(resultPage.PageHeader.PageType)} " +
                           $"Page {pageAddress}";

                    ScrollToOffset = null;

                    ApplyPageDisplay(display);

                    NextPage = new PageAddress(PageAddress.FileId, PageAddress.PageId + 1);

                    if (PageAddress.PageId > 0)
                    {
                        PreviousPage = new PageAddress(PageAddress.FileId, PageAddress.PageId - 1);
                    }

                    ReplayStatus = string.Empty;

                    ChangeSpans = [];
                    SelectedChangeSpan = null;
                    PfsBorders = null;

                    IsLoading = false;
                });
            }, CancellationToken);

        History.Add(PageAddress);
    }

    private void ApplyPageDisplay(PageDisplay display)
    {
        AllocationUnit = display.AllocationUnit;

        if (display.Records is { } records)
        {
            Records.Clear();
            Records.AddRange(records);

            RecordsResultSet = display.RecordsResultSet ?? new QueryResultSet();
        }

        if (display.AllocationFileId is { } allocationFileId)
        {
            AllocationFileId = allocationFileId;
        }

        if (display.AllocationStartPage is { } allocationStartPage)
        {
            AllocationStartPage = allocationStartPage;
        }

        if (display.AllocationLayer is { } allocationLayer)
        {
            AllocationLayers = [allocationLayer];
        }

        if (display.PfsChain is { } pfsChain)
        {
            PfsChain = pfsChain;
        }

        if (display.IsRowDataTabVisible is { } isRowDataTabVisible)
        {
            IsRowDataTabVisible = isRowDataTabVisible;
        }

        if (display.IsAllocationsTabVisible is { } isAllocationsTabVisible)
        {
            IsAllocationsTabVisible = isAllocationsTabVisible;
        }

        if (display.IsPfsTabVisible is { } isPfsTabVisible)
        {
            IsPfsTabVisible = isPfsTabVisible;
        }

        if (display.TabSwitch is { } tabSwitch && SelectedTabIndex == tabSwitch.From)
        {
            SelectedTabIndex = tabSwitch.To;
        }

        PageSlots = new ObservableCollection<PageSlot>(display.Slots);

        SelectedSlot = PageSlots.FirstOrDefault(s => s.Index == display.Slot) ?? display.Slots[0];
        SelectedMarker = null;

        Page = display.Page;

        AddPageHeaderMarkers();
    }

    /// <summary>
    /// Builds a bindable annotation, categorising the change by which page structure region it lands in
    /// </summary>
    private static LogRecordAnnotation CreateAnnotation(ChangeSpan change, int offsetTableStart)
    {
        var (category, colour) = change.Offset switch
        {
            < 96 => ("Page Header", "#0078D4"),
            var offset when offset >= offsetTableStart => ("Offset Table", "#107C10"),
            _ => ("Page Data", "#C19C00")
        };

        return new LogRecordAnnotation
        {
            Offset = change.Offset,
            Length = change.Length,
            Description = change.Description,
            ItemType = change.ItemType,
            Value = change.Value ?? string.Empty,
            Category = category,
            CategoryColour = colour
        };
    }

    /// <summary>
    /// Builds the border outlining the PFS map cells whose bytes the replayed records changed
    /// </summary>
    private static List<AllocationBorder> BuildPfsChangeBorders(IEnumerable<List<LogRecordAnnotation>> annotations,
                                                                PageAddress pageAddress)
    {
        var cells = new SortedSet<int>();

        foreach (var annotation in annotations.SelectMany(a => a))
        {
            for (var offset = annotation.Offset; offset < annotation.Offset + annotation.Length; offset++)
            {
                var cell = offset - PfsPageParser.PfsOffset;

                if (cell is >= 0 and < PfsPage.PfsInterval)
                {
                    cells.Add(cell);
                }
            }
        }

        if (cells.Count == 0)
        {
            return [];
        }

        var ranges = new List<TimedRange>();

        var from = -1;
        var previous = -1;

        foreach (var cell in cells)
        {
            if (from < 0)
            {
                from = cell;
            }
            else if (cell != previous + 1)
            {
                ranges.Add(new TimedRange(from, previous, 0, long.MaxValue));

                from = cell;
            }

            previous = cell;
        }

        ranges.Add(new TimedRange(from, previous, 0, long.MaxValue));

        return
        [
            new AllocationBorder(AllocationBorderScope.Page,
                                 pageAddress.FileId,
                                 System.Drawing.Color.OrangeRed,
                                 ranges)
        ];
    }

    partial void OnSelectedLogRecordChanged(LogRecordItem? value)
    {
        _ = ShowLogRecordState(value);
    }

    /// <summary>
    /// Selects the page slot a log record operated on, so it is highlighted and scrolled to in the hex view
    /// </summary>
    public void SelectSlotForRecord(LogRecordItem item)
    {
        if (item.Record.PageAddress != PageAddress)
        {
            return;
        }

        var slot = PageSlots.FirstOrDefault(s => s.Index == item.Record.SlotId);

        if (slot is not null)
        {
            SelectedSlot = slot;
        }
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
            return PageSlots.FirstOrDefault(s => s.Index == PageDisplayBuilder.PageHeaderSlot);
        }

        var offsetTableStart = PageData.Size - Page.PageHeader.SlotCount * 2;

        if (offset >= offsetTableStart)
        {
            var slotId = (PageData.Size - 1 - offset) / 2;

            return PageSlots.FirstOrDefault(s => s.Index == slotId);
        }

        return PageSlots.Where(s => s is { Index: >= 0, Offset: > 0 } && s.Offset <= offset)
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
                var currentSlot = SelectedSlot?.Index;

                var page = PageService.ParsePage(Database, PageAddress, baseline);

                var status = string.Empty;

                var annotations = new Dictionary<LogRecordItem, List<LogRecordAnnotation>>();

                if (target is not null && pageItems.Count > 0)
                {
                    LogRecordApplier.Rebase(page, pageItems.Select(i => i.Record).ToList());

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

                        var offsetTableStart = PageData.Size - page.PageHeader.SlotCount * 2;

                        annotations[pageItem] =
                        [
                            .. result.Changes
                                .Select(c => CreateAnnotation(c, offsetTableStart))
                        ];
                    }

                    page = PageService.ParsePage(Database, PageAddress, page.Data);

                    if (status.Length == 0)
                    {
                        status = $"Page at LSN {target.Record.Lsn.ToBinaryString()}";
                    }
                }

                var pfsBorders = page is PfsPage
                    ? BuildPfsChangeBorders(annotations.Values, page.PageAddress)
                    : null;

                var display = DisplayBuilder.Build(page, currentSlot, PageAddress);

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

                    LogRecords = [.. LogRecords];

                    ApplyPageDisplay(display);

                    PfsBorders = pfsBorders;

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

    private void AddPageMarkers(string suffix)
    {
        MarkerTabName = $"{Page.PageHeader.PageTypeName}{suffix}";

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

        Markers = [.. pageMarkers.Concat(recordMarkers).OrderBy(o => o.StartPosition)];
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
            BackColour = Color.FromArgb(50, 205, 250, 205),
            IsVisible = true
        });

        return m;
    }
}
