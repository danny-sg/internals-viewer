using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InternalsViewer.UI.App.ViewModels.Index;

public sealed class IndexTabViewModelFactory(ILogger<IndexTabViewModel> logger,
                                             IndexService indexService,
                                             IPageService pageService,
                                             IRecordService recordService)
{
    private IndexService IndexService { get; } = indexService;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    public IndexTabViewModel Create(DatabaseSource database)
        => new(logger, IndexService, RecordService, PageService, database);
}

public partial class IndexTabViewModel(ILogger<IndexTabViewModel> logger,
                                       IndexService indexService,
                                       IRecordService recordService,
                                       IPageService pageService,
                                       DatabaseSource database) : TabViewModel
{
    private ILogger<IndexTabViewModel> Logger { get; } = logger;

    private IndexService IndexService { get; } = indexService;

    private IRecordService RecordService { get; } = recordService;

    private IPageService PageService { get; } = pageService;

    public DatabaseSource Database { get; } = database;

    [ObservableProperty]
    private float _zoom = 1;

    [ObservableProperty]
    private bool _isZoomToFit = true;

    [ObservableProperty]
    private PageAddress _rootPage;

    [ObservableProperty]
    private List<IndexNode> _nodes = [];

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private int _loadedPageCount;

    [ObservableProperty]
    private long _totalPageCount;

    [ObservableProperty]
    private AllocationUnit? _allocationUnit;

    public string ProgressText => TotalPageCount > 0
        ? $"{LoadedPageCount:N0} / {TotalPageCount:N0} pages"
        : $"{LoadedPageCount:N0} pages";

    public bool IsProgressVisible => TotalPageCount >= IndexService.ProgressReportInterval;

    public double ProgressMaximum => TotalPageCount;

    public string LoadingText => string.IsNullOrEmpty(AllocationUnit?.IndexName)
        ? "Loading Index..."
        : $"Loading {AllocationUnit.IndexName}...";

    partial void OnAllocationUnitChanged(AllocationUnit? value)
    {
        TotalPageCount = value?.UsedPages ?? 0;

        OnPropertyChanged(nameof(LoadingText));
    }

    partial void OnLoadedPageCountChanged(int value) => OnPropertyChanged(nameof(ProgressText));

    partial void OnTotalPageCountChanged(long value)
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(IsProgressVisible));
        OnPropertyChanged(nameof(ProgressMaximum));
    }

    [ObservableProperty]
    private bool _isRecordsLoading;

    private const int RecordsSpinnerDelayMs = 100;

    [ObservableProperty]
    private string _objectIndexType = string.Empty;
    
    [ObservableProperty]
    private bool _isTooltipEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BodyColumnWidth))]
    [NotifyPropertyChangedFor(nameof(DetailColumnWidth))]
    [NotifyPropertyChangedFor(nameof(DetailSplitterVisibility))]
    private bool _isDetailPaneVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BodyColumnWidth))]
    [NotifyPropertyChangedFor(nameof(DetailColumnWidth))]
    [NotifyPropertyChangedFor(nameof(DetailSplitterVisibility))]
    private bool _isIndexDetailsVisible;

    private bool _isDetailsLinkPending = true;

    partial void OnIsDetailPaneVisibleChanged(bool value)
    {
        if (value && _isDetailsLinkPending)
        {
            _isDetailsLinkPending = false;

            IsIndexDetailsVisible = true;
        }
    }

    partial void OnIsIndexDetailsVisibleChanged(bool value) => _isDetailsLinkPending = false;

    public bool IsRightPaneVisible => IsDetailPaneVisible || IsIndexDetailsVisible;

    public GridLength BodyColumnWidth
        => IsRightPaneVisible ? new GridLength(6, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);

    public GridLength DetailColumnWidth
        => IsRightPaneVisible ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public Visibility DetailSplitterVisibility
        => IsRightPaneVisible ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> _records = [];

    [ObservableProperty]
    private PageAddress? _selectedPageAddress;

    [ObservableProperty]
    private IReadOnlyList<PageSpan> _pageSpans = [];

    [ObservableProperty]
    private long _playheadTimeUs;

    [ObservableProperty]
    private PageAddress? _selectedNextPage;

    [ObservableProperty]
    private PageAddress? _selectedPreviousPage;

    [ObservableProperty]
    private int? _selectedLevel;

    [ObservableProperty]
    private int? _selectedSlot;

    [ObservableProperty]
    private ObservableCollection<PageAddress> _highlightedPages = [];

    [RelayCommand]
    public async Task Refresh()
    {
        Logger.LogDebug("Refreshing Index Tab");

        LoadedPageCount = 0;

        IndexService.ProgressReportInterval = TotalPageCount > 100_000 ? 4096 : 1;

        OnPropertyChanged(nameof(IsProgressVisible));

        var progress = new Progress<int>(count => LoadedPageCount = count);

        // Worker thread
        await Task.Run(async () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    IsInitialized = false;
                });

                Logger.LogDebug("Getting nodes for index from root node: {RootPage}", RootPage);

                var result = await IndexService.GetNodes(Database, RootPage, CancellationToken, progress);

                Logger.LogDebug("{Count} node(s) found", result.Count);

                DispatcherQueue.TryEnqueue(() =>
                {
                    Logger.LogDebug("Updating UI");

                    Nodes = result;
                    IsInitialized = true;
                });
            }, CancellationToken);
    }

    [RelayCommand]
    public async Task LoadPage(PageAddress pageAddress)
    {
        Logger.LogDebug("Loading Index Page: {PageAddress}", pageAddress);

        if (pageAddress == PageAddress.Empty)
        {
            Logger.LogDebug("(Page Empty)");

            // Update via UI thread
            DispatcherQueue.TryEnqueue(() =>
            {
                IsDetailPaneVisible = false;

                SelectedLevel = null;
                SelectedNextPage = null;
                SelectedPreviousPage = null;
                SelectedPageAddress = pageAddress;

                HighlightedPages.Clear();
                Records.Clear();
            });

            return;
        }

        SelectedPageAddress = pageAddress;

        IsDetailPaneVisible = true;

        using var spinnerDelay = new CancellationTokenSource();

        _ = ShowRecordsSpinnerAfterDelay(spinnerDelay.Token);

        Internals.Engine.Pages.Page? page = null;

        List<IndexRecordModel> decodedRecords = [];

        // Worker thread
        await Task.Run(async () =>
            {
                Logger.LogDebug("Loading Page: {PageAddress}", pageAddress);

                page = await PageService.GetPage(Database, pageAddress, CancellationToken, false);

                if (page is IndexPage indexPage)
                {
                    Logger.LogDebug("Decoding Index Page records");

                    decodedRecords = GetIndexRecordModels(RecordService.GetIndexRecords(indexPage));
                }
                else if (page is DataPage dataPage)
                {
                    Logger.LogDebug("Decoding Data Page records");

                    decodedRecords = GetDataRecordModels(RecordService.GetDataRecords(dataPage));
                }
            }, CancellationToken);

        Logger.LogDebug("Decoded {Count} record(s)", decodedRecords.Count);

        await spinnerDelay.CancelAsync();

        Records = new ObservableCollection<IndexRecordModel>(decodedRecords);
        SelectedLevel = page?.PageHeader.Level;
        SelectedNextPage = page?.PageHeader.NextPage;
        SelectedPreviousPage = page?.PageHeader.PreviousPage;

        IsRecordsLoading = false;

        IsDetailPaneVisible = true;
    }

    private async Task ShowRecordsSpinnerAfterDelay(CancellationToken token)
    {
        try
        {
            await Task.Delay(RecordsSpinnerDelayMs, token);

            if (!token.IsCancellationRequested)
            {
                IsRecordsLoading = true;
            }
        }
        catch (TaskCanceledException)
        {
            // Load completed within the delay window
        }
    }

    partial void OnRootPageChanged(PageAddress value)
    {
        var allocationUnit = Database.AllocationUnits.Values.FirstOrDefault(a => a.RootPage == value);
        
        AllocationUnit = allocationUnit;

        Name = "Index: " + AllocationUnit?.IndexName;
    }

    private static List<IndexRecordModel> GetIndexRecordModels(IEnumerable<IIndexRecord> source)
    {
        var models = source.Select(r => new IndexRecordModel
        {
            Slot = r.Slot,
            DownPagePointer = r.DownPagePointer,
            RowIdentifier = r.Rid,
            Fields =
            [
                .. r.Fields.Select(f => new IndexRecordFieldModel
                {
                    Name = f.Name,
                    Value = f.Value,
                    DataType = f.ColumnStructure.DataType
                })
            ]
        }).ToList();

        return models;
    }

    private static List<IndexRecordModel> GetDataRecordModels(IEnumerable<IRecord> source)
    {
        var models = source.Select(r => new IndexRecordModel
        {
            Slot = r.Slot,
            Fields =
            [
                .. r.Fields.Select(f => new IndexRecordFieldModel
                {
                    Name = f.Name,
                    Value = f.Value,
                    DataType = f.ColumnStructure.DataType
                })
            ]
        }).ToList();

        return models;
    }

    public void SetHighlightedPage(PageAddress pageAddress)
    {
        if (pageAddress != PageAddress.Empty)
        {
            HighlightedPages = [pageAddress];
        }
        else
        {
            HighlightedPages = [];
        }
    }
}
