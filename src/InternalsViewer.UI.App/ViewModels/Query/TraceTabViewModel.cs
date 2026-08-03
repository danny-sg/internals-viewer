using System.Collections.Generic;
using System.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.Plan;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.UI.App.Models.Trace;
using InternalsViewer.UI.App.Services.Trace;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.ViewModels.Index;
using InternalsViewer.UI.App.Views.Query.Tabs.Trace;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed class TraceTabViewModelFactory(IIteratorFactory iteratorFactory, IndexService indexService)
{
    /// <summary>
    /// Builds a trace of an operator and everything below it, or null when some operator in that tree cannot be simulated
    /// </summary>
    public TraceTabViewModel? Create(DatabaseSource database,
                                     PlanNode node,
                                     Func<PlanNode, AllocationUnit?> resolveUnit,
                                     DateTime? queryTime,
                                     ScanModeResult? scanMode)
    {
        var builder = new TraceDefinitionBuilder(resolveUnit);

        if (builder.Build(node) is not { } definition)
        {
            return null;
        }

        var visuals = TraceSourceCollector.Collect(definition)
                                          .Select(s => CreateVisual(database, s, builder))
                                          .OfType<TraceVisualViewModel>()
                                          .ToList();

        if (visuals.Count == 0)
        {
            return null;
        }

        var iterator = iteratorFactory.Create(definition);

        return new TraceTabViewModel(iterator, definition, database, node, queryTime, scanMode, visuals);
    }

    private TraceVisualViewModel? CreateVisual(DatabaseSource database, TraceSource source, TraceDefinitionBuilder builder)
    {
        if (!builder.Units.TryGetValue(source.NodeId, out var unit))
        {
            return null;
        }

        var kind = source.Kind == TraceSourceKind.Index ? TraceVisualKind.Index : TraceVisualKind.Allocation;

        var title = source.Role == TraceSourceRole.None ? DisplayName(unit) : $"{source.Role}: {DisplayName(unit)}";

        var node = builder.Nodes.GetValueOrDefault(source.NodeId);

        return new TraceVisualViewModel(kind, database, unit, indexService, title, source.NodeId)
        {
            IsSideStackVisible = source.Role != TraceSourceRole.None,
            ShowObjectBorderImmediately = source.Kind == TraceSourceKind.Heap,
            IsHashTableVisible = source.Role == TraceSourceRole.Build,
            ColumnFilter = RecordColumnFilter.For(node, includesBookmark: source.Role == TraceSourceRole.Lookup)
        };
    }

    private static string DisplayName(AllocationUnit unit)
        => string.IsNullOrEmpty(unit.IndexName) ? unit.TableName : unit.IndexName;
}

public sealed partial class TraceTabViewModel : ObservableObject
{
    private const int RunStepDelayMs = 150;

    private const int HistoryLimit = 1000;

    private const int BucketColumn = 0;

    private const int HashColumn = 1;

    private const int FirstValueColumn = 2;

    public TraceTabViewModel(IStepIterator iterator,
                             IteratorDefinition definition,
                             DatabaseSource database,
                             PlanNode? planNode,
                             DateTime? queryTime,
                             ScanModeResult? scanMode,
                             IReadOnlyList<TraceVisualViewModel> visuals)
    {
        Iterator = iterator;
        Definition = definition;
        Database = database;
        PlanNode = planNode;
        QueryTime = queryTime;
        ScanMode = scanMode;
        Visuals = visuals;
        VisualsBySource = visuals.ToDictionary(v => v.Source);
        DefinitionBySource = TraceSourceCollector.Collect(definition).ToDictionary(s => s.NodeId, s => s.Definition);

        Operators = [.. TraceSourceCollector.CollectOperators(definition)
                                            .Select(o => new TraceOperatorViewModel(o.NodeId, OperatorTitle(o), OperatorDescription(o)))];

        OperatorsBySource = Operators.ToDictionary(o => o.NodeId);

        SelectedVisual = visuals[0];

        if (Visuals.FirstOrDefault(v => v.IsHashTableVisible) is { } buildVisual)
        {
            buildVisual.PropertyChanged += OnBuildVisualPropertyChanged;
        }

        Dock = BuildDock();

        Dock.LayoutChanged += (_, _) => OnDockLayoutChanged();

        Strategy = SeekDescription;
    }

    private static string OperatorTitle(JoinDefinition definition)
        => definition switch
        {
            HashMatchDefinition => "Hash Match",
            MergeJoinDefinition => "Merge Join",
            NestedLoopsDefinition => "Nested Loops",
            _ => "Results"
        };

    /// <summary>
    /// The join type and the columns matched on, stated the way the operator states them
    /// </summary>
    private static string OperatorDescription(JoinDefinition definition)
    {
        var keys = definition switch
        {
            HashMatchDefinition hash => hash.Build.JoinColumns,
            MergeJoinDefinition merge => merge.Outer.JoinColumns,
            _ => []
        };

        var on = keys.Count > 0 ? $" on {string.Join(", ", keys)}" : string.Empty;

        var residual = definition.Residual is null ? string.Empty : " with residual";

        return $"{definition.JoinType.ToDisplayName()}{on}{residual}";
    }

    public SvgImageSource? IconSource => PlanNode is null ? null : new SvgImageSource(PlanIconResolver.Resolve(PlanNode));

    public DockLayoutViewModel Dock { get; }

    /// <summary>
    /// Lays the trace out as the operator tree, each operator showing its two inputs above the rows it produced
    /// </summary>
    /// <remarks>
    /// A nested operator gets the same arrangement inside the pane its parent reads it from, so the results one join hands to another are
    /// visible in between rather than having to be inferred from two separate walks.
    /// </remarks>
    private DockLayoutViewModel BuildDock()
    {
        var steps = DocumentViewModel.Create<TraceStepsPanelView>("Trace", this, canClose: false, keepAlive: true, key: "Steps");
        var description = DocumentViewModel.Create<TraceDescriptionPanelView>("Description", this, keepAlive: true, key: "Description");
        var strategy = DocumentViewModel.Create<TraceStrategyPanelView>("Strategy", this, keepAlive: true, key: "Strategy");

        var results = DocumentViewModel.Create<TraceResultsPanelView>("Results", this, keepAlive: true, key: "Results");

        _resultsDocument = results;

        var right = new TabGroupNode(steps, description, results, strategy);

        return new DockLayoutViewModel(new SplitNode(Orientation.Horizontal, BuildOperatorNode(Definition), right));
    }

    private LayoutNode BuildOperatorNode(IteratorDefinition definition)
        => definition switch
        {
            UnaryDefinition unary
                => BuildOperatorNode(unary.Source),
            NestedLoopsDefinition loops
                => Compose(loops.NodeId, loops.Outer, loops.Inner),
            MergeJoinDefinition merge
                => Compose(merge.NodeId, merge.Outer.Source, merge.Inner.Source),
            HashMatchDefinition hash
                => Compose(hash.NodeId, hash.Build.Source, hash.Probe.Source),
            _ => new TabGroupNode(VisualDocument(definition.NodeId))
        };

    private LayoutNode Compose(int nodeId, IteratorDefinition left, IteratorDefinition right)
    {
        var inputs = new SplitNode(Orientation.Horizontal, BuildOperatorNode(left), BuildOperatorNode(right));

        if (!OperatorsBySource.TryGetValue(nodeId, out var operatorViewModel))
        {
            return inputs;
        }

        var results = DocumentViewModel.Create<TraceOperatorResultsPanelView>("Results",
                                                                             operatorViewModel,
                                                                             canClose: false,
                                                                             keepAlive: true,
                                                                             key: $"Operator{nodeId}");

        // A hash match keeps its table beside its results, because the table is what produced them
        var bottom = HashTableDocument(left, nodeId) is { } hashTable
            ? new TabGroupNode(hashTable, results)
            : new TabGroupNode(results);

        return new SplitNode(Orientation.Vertical, inputs, bottom);
    }

    /// <summary>
    /// The hash table of a hash match, taken from the build side that fills it
    /// </summary>
    private DocumentViewModel? HashTableDocument(IteratorDefinition build, int nodeId)
    {
        var source = TraceSourceCollector.Collect(build).FirstOrDefault();

        if (source is null || !VisualsBySource.TryGetValue(source.NodeId, out var visual) || !visual.IsHashTableVisible)
        {
            return null;
        }

        return DocumentViewModel.Create<TraceHashTablePanelView>("Hash Table",
                                                                 visual,
                                                                 canClose: false,
                                                                 keepAlive: true,
                                                                 key: $"HashTable{nodeId}");
    }

    private DocumentViewModel VisualDocument(int source)
    {
        var visual = VisualsBySource[source];

        return DocumentViewModel.Create<TraceVisualPanelView>(visual.Title,
                                                              visual,
                                                              canClose: false,
                                                              keepAlive: true,
                                                              key: $"Visual{source}");
    }

    public IReadOnlyList<TraceVisualViewModel> Visuals { get; }

    /// <summary>
    /// The tab whose walk the strategy and description panels describe
    /// </summary>
    [ObservableProperty]
    private TraceVisualViewModel _selectedVisual;

    private IteratorDefinition Definition { get; }

    private IStepIterator Iterator { get; }

    private Dictionary<int, TraceVisualViewModel> VisualsBySource { get; }

    public IReadOnlyList<TraceOperatorViewModel> Operators { get; }

    private Dictionary<int, TraceOperatorViewModel> OperatorsBySource { get; }

    public DatabaseSource Database { get; }

    public AllocationUnit AllocationUnit => SelectedVisual.AllocationUnit;

    [ObservableProperty]
    private PlanNode? _planNode;

    private DateTime? QueryTime { get; set; }

    [ObservableProperty]
    private ScanModeResult? _scanMode;

    public void Refresh(PlanNode planNode, DateTime? queryTime, ScanModeResult? scanMode)
    {
        PlanNode = planNode;
        QueryTime = queryTime;
        ScanMode = scanMode;

        ResetStep();

        OnPropertyChanged(nameof(SeekDescription));
        OnPropertyChanged(nameof(IconSource));
    }

    public event EventHandler<PageNavigatedEventArgs>? PageNavigated;

    private bool _hasNavigatedSinceReset;

    [ObservableProperty]
    private ObservableCollection<AccessStep> _stepHistory = [];

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> _resultRecords = [];


    [ObservableProperty]
    private AccessStep? _currentStep;

    [ObservableProperty]
    private AccessStrategy? _strategy;

    [ObservableProperty]
    private AccessStrategy? _innerStrategy;

    public string SelectedSectionTitle => SelectedVisual.Title;

    /// <summary>
    /// Whether the trace reads from more than one input, which is when a tab needs naming
    /// </summary>
    public bool HasMultipleSources => Visuals.Count > 1;

    /// <summary>
    /// What the join does with a pair that matches on both sides, or null when the trace is not a join
    /// </summary>
    public JoinDecision? JoinRule => Definition is JoinDefinition join ? join.JoinType.Decide(true, true) : null;

    public AccessStrategy? SelectedStrategy => StrategyBySource.GetValueOrDefault(SelectedVisual.Source);

    public AccessPhase? SelectedPhase
        => CurrentStep is { } step && step.Source == SelectedVisual.Source ? step.AccessPhase : null;

    /// <summary>
    /// True while the selected input is a correlated seek that has not yet been bound, so it has no descent to describe
    /// </summary>
    public bool IsSelectedStrategyPending
        => SelectedStrategy is null && IsStepping;

    private Dictionary<int, AccessStrategy?> StrategyBySource { get; } = [];

    private Dictionary<int, IteratorDefinition> DefinitionBySource { get; }

    /// <summary>
    /// Restricts a build row to the columns the build side states it outputs
    /// </summary>
    private RecordColumnFilter BuildColumnFilter
        => Visuals.FirstOrDefault(v => v.IsHashTableVisible)?.ColumnFilter ?? RecordColumnFilter.All;

    public Brush? OuterAccentBrush => Visuals.Count > 1 ? ToBrush(Visuals[0].ObjectColour) : null;

    public Brush? InnerAccentBrush => Visuals.Count > 1 ? ToBrush(Visuals[1].ObjectColour) : null;

    /// <summary>
    /// The colour a step should be marked with, found from the input that produced it
    /// </summary>
    public Brush? AccentFor(int source)
        => Visuals.Count > 1 && VisualsBySource.TryGetValue(source, out var visual) ? ToBrush(visual.ObjectColour) : null;

    private static Brush ToBrush(System.Drawing.Color colour)
    {
        return new SolidColorBrush(Windows.UI.Color.FromArgb(colour.A, colour.R, colour.G, colour.B));
    }

    [ObservableProperty]
    private bool _isStepping;

    [ObservableProperty]
    private bool _isStepComplete;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isRunningToEnd;

    [ObservableProperty]
    private long _playheadTimeUs;

    partial void OnPlayheadTimeUsChanged(long value)
    {
        foreach (var visual in Visuals)
        {
            visual.PlayheadTimeUs = value;
        }
    }

    private DocumentViewModel? _resultsDocument;

    private CancellationTokenSource? _runToEndCancellation;

    [ObservableProperty]
    private bool _isResultsVisible = true;

    private bool _suppressResultsSync;

    public string ResultsLabel => ResultRecords.Count > 0 ? $"Results ({ResultRecords.Count:N0})" : "Results";

    partial void OnIsResultsVisibleChanged(bool value)
    {
        if (_suppressResultsSync || _resultsDocument is not { } document)
        {
            return;
        }

        if (value)
        {
            Dock.Show(document);
        }
        else
        {
            Dock.Close(document);
        }
    }

    private void OnDockLayoutChanged()
    {
        if (_resultsDocument is not { } document)
        {
            return;
        }

        _suppressResultsSync = true;

        IsResultsVisible = Dock.Contains(document);

        _suppressResultsSync = false;
    }

    private void UpdateResultsTitle()
    {
        OnPropertyChanged(nameof(ResultsLabel));
    }

    public AccessPhase? CurrentPhase => CurrentStep?.AccessPhase;

    public AccessCounters CurrentCounters => CurrentStep?.Counters ?? default;

    public bool IsWalkInProgress => IsStepping && !IsStepComplete;

    /// <summary>
    /// The strategy the selected input will use, worked out before the walk starts
    /// </summary>
    /// <remarks>
    /// Once opened the iterator publishes what it actually settled on, which supersedes this. Until then the definition already says
    /// enough to describe the descent, which is what makes the panel useful before anything has been stepped.
    /// </remarks>
    public AccessStrategy? SeekDescription
    {
        get
        {
            if (!DefinitionBySource.TryGetValue(SelectedVisual.Source, out var definition))
            {
                return null;
            }

            if (definition is AllocationScanDefinition allocation)
            {
                return AccessStrategyBuilder.BuildAllocationScan(allocation.Residual,
                                                                 allocation.RowGoal,
                                                                 hasUntranslatedResidual: allocation.HasUntranslatedResidual) with
                {
                    EntryPoint = allocation.FirstIamPage,
                    EntryPointSource = "sys.sysallocunits.pgfirstiam"
                };
            }

            if (definition is not RangeDefinition range)
            {
                return null;
            }

            var structure = IndexStructureProvider.GetIndexStructure(Database, range.AllocationUnitId);

            return AccessStrategyBuilder.Build(structure,
                                               range.Ranges.Count > 0 ? range.Ranges[0] : SeekBounds.All,
                                               range.Direction,
                                               range.RowGoal,
                                               range.Residual,
                                               ranges: range.Ranges,
                                               hasUntranslatedResidual: range.HasUntranslatedResidual) with
            {
                EntryPoint = range.RootPage,
                EntryPointSource = "sys.sysallocunits.pgroot"
            };
        }
    }

    private ScanDirection ScanDirection
        => PlanNode?.ScanInfo?.IsForward == false ? ScanDirection.Backward : ScanDirection.Forward;

    partial void OnCurrentStepChanged(AccessStep? value)
    {
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(SelectedPhase));
        OnPropertyChanged(nameof(CurrentCounters));
    }

    partial void OnIsSteppingChanged(bool value) => OnPropertyChanged(nameof(IsWalkInProgress));

    partial void OnIsStepCompleteChanged(bool value) => OnPropertyChanged(nameof(IsWalkInProgress));

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task Run()
    {
        if (IsRunning)
        {
            IsRunning = false;

            return;
        }

        IsRunning = true;

        while (IsRunning && !IsStepComplete)
        {
            await StepNext();

            await Task.Delay(CurrentStep?.AccessPhase == AccessPhase.Walk ? RunStepDelayMs / 10 : RunStepDelayMs);
        }

        IsRunning = false;
    }

    [RelayCommand]
    public async Task StepNext()
    {
        if (!IsStepping)
        {
            await StartAsync();
        }

        var step = await Task.Run(() => Iterator.StepNextAsync(CancellationToken.None));

        if (step is null)
        {
            IsStepComplete = true;
            IsRunning = false;

            return;
        }

        Append(step, StepHistory);

        if (step is AccessStep.JoinEmit pair && OperatorsBySource.TryGetValue(step.Source, out var emitting))
        {
            emitting.Add(ToJoinedModel(pair));
        }

        if (ToResultModel(step) is { } resultModel)
        {
            ResultRecords.Add(resultModel);

            UpdateResultsTitle();
        }

        UpdateStrategies();

        SyncSideBuffers();

        SyncHashTable(step);

        GetVisual(step.Source)?.Apply(step);

        CurrentStep = step;

        var readPage = step switch
        {
            AccessStep.ReadPage read => read.PageAddress,
            AccessStep.IamRead iam => iam.PageAddress,
            AccessStep.PfsRead pfs => pfs.PageAddress,
            _ => (PageAddress?)null
        };

        if (readPage is { } pageAddress)
        {
            PageNavigated?.Invoke(this, new PageNavigatedEventArgs(pageAddress, !_hasNavigatedSinceReset)
            {
                Selection = SelectedVisual.Kind == TraceVisualKind.Allocation ? PageReadSelection.Last : PageReadSelection.Next
            });

            _hasNavigatedSinceReset = true;
        }

        if (step is AccessStep.Stopped)
        {
            IsStepComplete = true;
            IsRunning = false;
        }

        UpdateActiveVisual(step);
    }

    private void UpdateActiveVisual(AccessStep? step)
    {
        var isSideStep = step is not null && Visuals.Any(v => v.Source == step.Source);

        foreach (var visual in Visuals)
        {
            visual.IsDimmed = Visuals.Count > 1
                              && isSideStep
                              && !IsStepComplete
                              && !IsRunning
                              && !IsRunningToEnd
                              && visual.Source != step!.Source;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task RunToEnd()
    {
        if (IsRunningToEnd)
        {
            _runToEndCancellation?.Cancel();

            return;
        }

        IsRunningToEnd = true;

        _runToEndCancellation = new CancellationTokenSource();

        var cancellationToken = _runToEndCancellation.Token;

        try
        {
            if (!IsStepping)
            {
                await StartAsync();
            }

            var steps = new ObservableCollection<AccessStep>();

            var results = new ObservableCollection<IndexRecordModel>();

            var replays = new Dictionary<TraceVisualViewModel, TraceVisualReplay>();

            await Task.Run(async () =>
            {
                try
                {
                    while (await Iterator.StepNextAsync(cancellationToken) is not null)
                    {
                    }
                }
                catch (OperationCanceledException)
                {
                }

                foreach (var step in Iterator.History)
                {
                    Append(step, steps);

                    if (ToResultModel(step) is { } resultModel)
                    {
                        results.Add(resultModel);
                    }
                }

                foreach (var visual in Visuals)
                {
                    replays[visual] = visual.ComputeReplay(Iterator.History);
                }
            }, CancellationToken.None);

            StepHistory = steps;
            ResultRecords = results;

            UpdateResultsTitle();
            UpdateStrategies();

            foreach (var visual in Visuals)
            {
                visual.ApplyReplay(replays[visual]);
            }

            SyncSideBuffers();

            SyncHashTable(Iterator.Current);

            CurrentStep = Iterator.Current;
            IsStepComplete = Iterator.IsComplete;

            UpdateActiveVisual(CurrentStep);
        }
        finally
        {
            IsRunningToEnd = false;

            _runToEndCancellation.Dispose();
            _runToEndCancellation = null;
        }
    }

    [RelayCommand]
    public void ResetStep()
    {
        _runToEndCancellation?.Cancel();

        IsRunning = false;
        IsRunningToEnd = false;
        IsStepping = false;
        IsStepComplete = false;

        _hasNavigatedSinceReset = false;

        StepHistory = [];
        ResultRecords = [];

        foreach (var operatorViewModel in Operators)
        {
            operatorViewModel.Clear();
        }

        _hashBucketModels = null;
        _syncedHashRowCount = 0;
        _currentHashBucket = null;
        _currentHashEntry = null;
        CurrentStep = null;
        Strategy = SeekDescription;
        InnerStrategy = null;

        _syncedBuffers.Clear();

        foreach (var visual in Visuals)
        {
            visual.Reset();
        }

        UpdateResultsTitle();
    }

    private TraceVisualViewModel? GetVisual(int source)
    {
        return Visuals.FirstOrDefault(v => v.Source == source);
    }

    /// <summary>
    /// Refreshes each side's row pane from the rows the join is currently holding
    /// </summary>
    /// <remarks>
    /// Taken from the join rather than accumulated from the row steps, because only the join knows which rows it still holds - a row it
    /// has advanced past is gone, and a row read ahead of the current key is not yet in play.
    /// </remarks>
    private void SyncSideBuffers()
    {
        if (Iterator is not IJoinStepIterator join)
        {
            return;
        }

        foreach (var visual in Visuals)
        {
            if (!visual.IsSideStackVisible || visual.IsHashTableVisible)
            {
                continue;
            }

            var buffer = visual.Source == NestedLoopsStepIterator.OuterSource ? join.Outer.Buffer : join.Inner.Buffer;

            if (_syncedBuffers.TryGetValue(visual.Source, out var synced) && synced.SequenceEqual(buffer))
            {
                continue;
            }

            _syncedBuffers[visual.Source] = [.. buffer];

            visual.SideRecords = [.. buffer.Select(ToRecordModel)];
        }
    }

    private readonly Dictionary<int, List<JoinBufferRow>> _syncedBuffers = new();

    private void OnBuildVisualPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TraceVisualViewModel.HashBucketCount)
            || sender is not TraceVisualViewModel visual
            || Iterator is not HashMatchStepIterator hashService)
        {
            return;
        }

        hashService.SetBucketCount(visual.HashBucketCount);

        SyncHashTable(CurrentStep);
    }

    private void SyncHashTable(AccessStep? step)
    {
        if (Iterator is not HashMatchStepIterator hashService)
        {
            return;
        }

        var visual = Visuals.FirstOrDefault(v => v.IsHashTableVisible);

        if (visual is null)
        {
            return;
        }

        var table = hashService.Table;

        if (_hashBucketModels is { } models
            && models.Count == table.BucketCount
            && table.RowCount == _syncedHashRowCount + 1
            && step is AccessStep.HashBuild { IsNullKey: false } build)
        {
            models[build.Bucket].Entries.Add(ToEntryModel(table.Buckets[build.Bucket].Entries[build.Entry],
                                                          build.Bucket,
                                                          build.Entry));

            _syncedHashRowCount = table.RowCount;
        }
        else if (_hashBucketModels is null
                 || _hashBucketModels.Count != table.BucketCount
                 || _syncedHashRowCount != table.RowCount)
        {
            RebuildHashBuckets(table);

            visual.HashBuckets = _hashBucketModels!;
        }

        UpdateHashHighlight(step, table);

        visual.HashTableSummary = hashService.BuildRowEstimate > 0
            ? $"{table.RowCount:N0} rows, sized for {hashService.BuildRowEstimate:N0}, "
              + $"{table.BucketCount} buckets, longest chain {table.LongestChain}"
            : $"{table.RowCount:N0} rows, {table.BucketCount} buckets, longest chain {table.LongestChain}";
    }

    private List<HashBucketModel>? _hashBucketModels;

    private int _syncedHashRowCount;

    private HashBucketModel? _currentHashBucket;

    private HashEntryModel? _currentHashEntry;

    private readonly List<HashEntryModel> _matchedHashEntries = [];

    private void RebuildHashBuckets(HashTable table)
    {
        _currentHashBucket = null;
        _currentHashEntry = null;

        _matchedHashEntries.Clear();

        var models = new List<HashBucketModel>(table.BucketCount);

        foreach (var bucket in table.Buckets)
        {
            var model = new HashBucketModel { Index = bucket.Index };

            foreach (var entry in bucket.Entries)
            {
                model.Entries.Add(ToEntryModel(entry, bucket.Index, model.Entries.Count));
            }

            models.Add(model);
        }

        _hashBucketModels = models;
        _syncedHashRowCount = table.RowCount;
    }

    private void UpdateHashHighlight(AccessStep? step, HashTable table)
    {
        // A new probe row starts a fresh verdict, so whatever the last one matched stops being green
        if (step is AccessStep.HashProbe or AccessStep.HashBuild)
        {
            ClearMatchedHashEntries();
        }

        if (_currentHashBucket is not null)
        {
            _currentHashBucket.IsCurrent = false;

            _currentHashBucket = null;
        }

        if (_currentHashEntry is not null)
        {
            _currentHashEntry.IsCurrent = false;

            _currentHashEntry = null;
        }

        var (bucketIndex, entryIndex) = step switch
        {
            AccessStep.HashBuild build => (build.Bucket, build.Entry),
            AccessStep.HashProbe probe => (probe.Bucket, -1),
            AccessStep.HashCompare compare => (compare.Bucket, compare.Entry),
            _ => (-1, -1)
        };

        if (_hashBucketModels is not { } models || bucketIndex < 0 || bucketIndex >= models.Count)
        {
            return;
        }

        _currentHashBucket = models[bucketIndex];
        _currentHashBucket.IsCurrent = true;

        if (entryIndex < 0 || entryIndex >= _currentHashBucket.Entries.Count)
        {
            return;
        }

        _currentHashEntry = _currentHashBucket.Entries[entryIndex];
        _currentHashEntry.IsCurrent = true;

        // The table's own matched flag stays set for the outer join drain, so the green here follows this comparison alone
        if (step is AccessStep.HashCompare { IsMatch: true })
        {
            _currentHashEntry.IsMatched = true;

            _matchedHashEntries.Add(_currentHashEntry);
        }
    }

    private void ClearMatchedHashEntries()
    {
        foreach (var entry in _matchedHashEntries)
        {
            entry.IsMatched = false;
        }

        _matchedHashEntries.Clear();
    }

    private HashEntryModel ToEntryModel(HashEntry entry, int bucketIndex, int entryIndex)
    {
        var record = TraceVisualViewModel.ToRecordModel(entry.Record, BuildColumnFilter);

        var columns = EnsureHashColumns(record);

        var cells = new List<HashCellModel>(columns.Count)
        {
            // Only the row that opens a bucket names it, so a chain reads as one bucket rather than the number repeating
            new() { Value = entryIndex == 0 ? bucketIndex.ToString() : string.Empty, Column = columns[BucketColumn] },
            new() { Value = $"{entry.Hash:X8}", Column = columns[HashColumn] }
        };

        for (var index = FirstValueColumn; index < columns.Count; index++)
        {
            var column = columns[index];

            var field = record.Fields.FirstOrDefault(f => string.Equals(f.Name, column.Header, StringComparison.OrdinalIgnoreCase));

            cells.Add(new HashCellModel { Value = field?.Value ?? string.Empty, Column = column });
        }

        return new HashEntryModel { Cells = cells };
    }

    /// <summary>
    /// Widens the grid to the columns a build row actually carries, which are not known until the first row is read
    /// </summary>
    /// <remarks>
    /// The base columns exist so the header is there before the build starts. Every row of a given build side carries the same columns, so
    /// this settles on the first row and the rest line up under it.
    /// </remarks>
    private IReadOnlyList<HashColumnModel> EnsureHashColumns(IndexRecordModel record)
    {
        var visual = Visuals.FirstOrDefault(v => v.IsHashTableVisible);

        if (visual is null)
        {
            return HashColumnModel.CreateBaseColumns();
        }

        if (visual.HashColumns.Count > FirstValueColumn)
        {
            return visual.HashColumns;
        }

        var columns = new List<HashColumnModel>(HashColumnModel.CreateBaseColumns());

        columns.AddRange(record.Fields.Select(f => new HashColumnModel { Header = f.Name }));

        visual.HashColumns = columns;

        return columns;
    }

    private static IndexRecordModel ToRecordModel(JoinBufferRow row)
    {
        var model = ToRecordModel(row.Record);

        model.IsMatched = row.IsMatched;

        return model;
    }

    private IndexRecordModel? ToResultModel(AccessStep step)
    {
        if (Definition is JoinDefinition)
        {
            return step is AccessStep.JoinEmit emit ? ToJoinedModel(emit) : null;
        }

        if (step is not AccessStep.Row { EmittedRecord: { } emitted })
        {
            return null;
        }

        return ToRecordModel(emitted);
    }

    /// <summary>
    /// Takes the strategy each input settled on once it was opened, so a tab can show its own rather than the tree's
    /// </summary>
    /// <remarks>
    /// A correlated inner has no strategy until its first rebind plans a descent, so this is called again as the walk proceeds and leaves
    /// what it already found alone.
    /// </remarks>
    private void UpdateStrategies()
    {
        foreach (var (source, strategy) in Collect(Iterator))
        {
            if (strategy is not null && VisualsBySource.ContainsKey(source))
            {
                StrategyBySource[source] = strategy;
            }
        }

        OnPropertyChanged(nameof(SelectedStrategy));

        IEnumerable<(int Source, AccessStrategy? Strategy)> Collect(IStepIterator iterator)
        {
            yield return (iterator.IteratorId, iterator.Strategy);

            if (iterator is not IJoinStepIterator join)
            {
                yield break;
            }

            foreach (var found in Collect(join.Outer.Iterator).Concat(Collect(join.Inner.Iterator)))
            {
                yield return found;
            }
        }
    }

    private async Task StartAsync()
    {
        var evaluationContext = QueryTime is { } queryTime ? new EvaluationContext(queryTime) : EvaluationContext.Now;

        var context = new IteratorContext(Database) { EvaluationContext = evaluationContext };

        await Task.Run(() => Iterator.OpenAsync(context, Definition, CancellationToken.None));

        Strategy = Iterator.Strategy;

        UpdateStrategies();

        if (Iterator is HashMatchStepIterator hash
            && Visuals.FirstOrDefault(v => v.IsHashTableVisible) is { } buildVisual)
        {
            buildVisual.HashBucketCount = hash.Table.BucketCount;
        }

        IsStepping = true;
    }

    /// <summary>
    /// Builds the joined output row from the columns the join operator states it returns
    /// </summary>
    /// <remarks>
    /// The operator's output list is what the join actually hands upwards, so it fixes both the columns and their order. Anything a side
    /// read only to do its own work, a bookmark most of all, is left out. A column the preserved side of an outer join has no row for
    /// reads as NULL, which also keeps the grid's columns steady from row to row.
    /// </remarks>
    private IndexRecordModel ToJoinedModel(AccessStep.JoinEmit emit)
    {
        var outputColumns = PlanNode?.OutputColumns ?? [];

        var fields = outputColumns.Count > 0
            ? [.. outputColumns.Select(c => ToField(emit, c))]
            : Combine(emit);

        return new IndexRecordModel
        {
            Slot = emit.OuterRecord?.Slot ?? emit.InnerRecord?.Slot ?? 0,
            RowIdentifier = null,
            Fields = fields
        };
    }

    private IndexRecordFieldModel ToField(AccessStep.JoinEmit emit, ColumnReference column)
    {
        var name = column.Column.Trim('[', ']');

        var table = column.Table.Trim('[', ']');

        // Where both sides carry the column, the output list says which table it came from
        var preferInner = Visuals.Count > 1
                          && !string.IsNullOrEmpty(table)
                          && string.Equals(table, Visuals[^1].AllocationUnit.TableName, StringComparison.OrdinalIgnoreCase);

        var field = preferInner
            ? Find(emit.InnerRecord, name) ?? Find(emit.OuterRecord, name)
            : Find(emit.OuterRecord, name) ?? Find(emit.InnerRecord, name);

        return new IndexRecordFieldModel
        {
            Name = name,
            Value = field?.Value ?? "NULL",
            DataType = field?.ColumnStructure.DataType ?? SqlDbType.Variant
        };
    }

    private static List<IndexRecordFieldModel> Combine(AccessStep.JoinEmit emit)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var fields = new List<IndexRecordFieldModel>();

        foreach (var record in new[] { emit.OuterRecord, emit.InnerRecord })
        {
            foreach (var field in record?.Fields ?? [])
            {
                fields.Add(new IndexRecordFieldModel
                {
                    Name = names.Add(field.Name) ? field.Name : $"Inner.{field.Name}",
                    Value = field.Value,
                    DataType = field.ColumnStructure.DataType
                });
            }
        }

        return fields;
    }

    private static RecordField? Find(IRecord? record, string name)
        => record?.Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    private static IndexRecordModel ToRecordModel(IRecord record)
    {
        return TraceVisualViewModel.ToRecordModel(record);
    }

    private static void Append(AccessStep step, ObservableCollection<AccessStep> history)
    {
        if (step is AccessStep.MergeCompare { Comparison: not 0 } compare
            && history.Count > 1
            && history[0] is AccessStep.Row or AccessStep.RowRun)
        {
            if (history[1] is AccessStep.MergeCompare previous
                && Math.Sign(previous.Comparison) == Math.Sign(compare.Comparison)
                && StaticKeyMatches(compare.Comparison, previous.OuterKey, previous.InnerKey, compare))
            {
                history[1] = new AccessStep.MergeCompareRun(Math.Sign(compare.Comparison), 2)
                {
                    OuterFrom = previous.OuterKey,
                    OuterTo = compare.OuterKey,
                    InnerFrom = previous.InnerKey,
                    InnerTo = compare.InnerKey,
                    Action = compare.Action,
                    Source = compare.Source,
                    Counters = compare.Counters
                };

                return;
            }

            if (history[1] is AccessStep.MergeCompareRun run
                && run.Comparison == Math.Sign(compare.Comparison)
                && StaticKeyMatches(compare.Comparison, run.OuterTo, run.InnerTo, compare))
            {
                history[1] = run with
                {
                    Count = run.Count + 1,
                    OuterTo = compare.OuterKey,
                    InnerTo = compare.InnerKey,
                    Counters = compare.Counters
                };

                return;
            }
        }

        if (step is AccessStep.Probe probe)
        {
            if (history.Count > 0 && history[0] is AccessStep.ProbeRun probeRun && probeRun.Source == probe.Source)
            {
                history[0] = new AccessStep.ProbeRun([probe, .. probeRun.Probes])
                {
                    Source = probe.Source,
                    Counters = probe.Counters
                };

                return;
            }

            step = new AccessStep.ProbeRun([probe])
            {
                Source = probe.Source,
                Counters = probe.Counters
            };
        }

        if (step is AccessStep.Row row && history.Count > 0)
        {
            var latest = history[0];

            if (latest is AccessStep.Row previous
                && previous.Source == row.Source
                && previous.Outcome == row.Outcome
                && previous.IsReadAhead == row.IsReadAhead
                && Math.Abs(row.Slot - previous.Slot) == 1)
            {
                history[0] = new AccessStep.RowRun(previous.Slot, row.Slot, row.Outcome)
                {
                    Count = 2,
                    HasResidual = row.HasResidual,
                    HasRange = row.HasRange,
                    EmitCount = EmitOf(previous) + EmitOf(row),
                    Counters = row.Counters,
                    Source = row.Source
                };

                return;
            }

            if (latest is AccessStep.RowRun run
                && run.Source == row.Source
                && run.Outcome == row.Outcome
                && !row.IsReadAhead
                && Math.Abs(row.Slot - run.ToSlot) == 1)
            {
                history[0] = run with
                {
                    ToSlot = row.Slot,
                    Count = run.Count + 1,
                    EmitCount = run.EmitCount + EmitOf(row),
                    Counters = row.Counters
                };

                return;
            }
        }

        history.Insert(0, step);

        if (history.Count > HistoryLimit)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    private static int EmitOf(AccessStep.Row row)
    {
        return row.Outcome == RowOutcome.Match ? 1 : 0;
    }

    private static bool StaticKeyMatches(int comparison, AccessKey previousOuter, AccessKey previousInner, AccessStep.MergeCompare compare)
    {
        return comparison < 0 ? previousInner.Equals(compare.InnerKey) : previousOuter.Equals(compare.OuterKey);
    }
}
