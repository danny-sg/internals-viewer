using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media.Imaging;
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
                                             IRecordService recordService,
                                             IndexStepService indexStepService)
{
    private IndexService IndexService { get; } = indexService;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    private IndexStepService IndexStepService { get; } = indexStepService;

    public IndexTabViewModel Create(DatabaseSource database)
        => new(logger, IndexService, RecordService, PageService, IndexStepService, database);
}

public partial class IndexTabViewModel(ILogger<IndexTabViewModel> logger,
                                       IndexService indexService,
                                       IRecordService recordService,
                                       IPageService pageService,
                                       IndexStepService indexStepService,
                                       DatabaseSource database) : TabViewModel
{
    private ILogger<IndexTabViewModel> Logger { get; } = logger;

    private IndexService IndexService { get; } = indexService;

    private IRecordService RecordService { get; } = recordService;

    private IPageService PageService { get; } = pageService;

    private IndexStepService IndexStepService { get; } = indexStepService;

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

    partial void OnAllocationUnitChanged(AllocationUnit? value) => OnPropertyChanged(nameof(LoadingText));

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

    public GridLength BodyColumnWidth
        => IsDetailPaneVisible ? new GridLength(6, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);

    public GridLength DetailColumnWidth
        => IsDetailPaneVisible ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public Visibility DetailSplitterVisibility
        => IsDetailPaneVisible ? Visibility.Visible : Visibility.Collapsed;

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
    private ObservableCollection<PageAddress> _highlightedPages = [];

    [ObservableProperty]
    private PlanNode? _planNode;

    [ObservableProperty]
    private ObservableCollection<AccessStep> _stepHistory = [];

    [ObservableProperty]
    private AccessStep? _currentStep;

    [ObservableProperty]
    private bool _isStepping;

    [ObservableProperty]
    private bool _isStepComplete;

    partial void OnPlanNodeChanged(PlanNode? value)
    {
        OnPropertyChanged(nameof(PredicateText));
        OnPropertyChanged(nameof(IconSource));
        OnPropertyChanged(nameof(HasPredicate));
    }

    public PredicateText? PredicateText => PlanNode?.GetText();

    public bool HasPredicate => (PredicateText?.Tokens.Length ?? 0) > 0;

    public SvgImageSource? IconSource => PlanNode is null ? null : new SvgImageSource(PlanIconResolver.Resolve(PlanNode));

    [RelayCommand]
    public async Task Refresh()
    {
        Logger.LogDebug("Refreshing Index Tab");

        LoadedPageCount = 0;

        var progress = new Progress<int>(count => LoadedPageCount = count);

        // Worker thread
        await Task.Run(async () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    IsInitialized = false;
                });

                IndexService.ProgressReportInterval = TotalPageCount > 100_000 ? 4096 : 1;

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

                page = await PageService.GetPage(Database, pageAddress, CancellationToken);

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

    [RelayCommand]
    public void ToggleStep()
    {
        IsDetailPaneVisible = !IsDetailPaneVisible;
    }

    [RelayCommand]
    public async Task StartStep()
    {
        if (PlanNode?.PredicateInfo is not { HasSeekBounds: true } predicateInfo || AllocationUnit is null)
        {
            Logger.LogDebug("No seek predicate available to step through");

            return;
        }

        StepHistory.Clear();
        CurrentStep = null;
        IsStepComplete = false;

        var bounds = predicateInfo.SeekBounds[0];

        await IndexStepService.StartAsync(Database,
                                          AllocationUnit.AllocationUnitId,
                                          RootPage,
                                          bounds,
                                          predicateInfo.Residual,
                                          ScanDirection.Forward,
                                          CancellationToken);

        IsStepping = true;

        await StepNext();
    }

    [RelayCommand]
    public async Task StepNext()
    {
        if (!IsStepping || IsStepComplete)
        {
            return;
        }

        var step = await IndexStepService.StepNextAsync(CancellationToken);

        if (step is null)
        {
            IsStepComplete = true;

            return;
        }

        StepHistory.Insert(0, step);

        CurrentStep = step;

        if (step is AccessStep.Stopped)
        {
            IsStepComplete = true;
        }

        var pageAddress = IndexStepService.CurrentPageAddress;

        if (pageAddress is not null && pageAddress != SelectedPageAddress)
        {
            await LoadPage(pageAddress.Value);
        }
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
