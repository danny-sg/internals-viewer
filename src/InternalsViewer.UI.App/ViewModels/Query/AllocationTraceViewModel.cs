using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Services.Allocations;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.UI.App.Models.Index;
using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.ViewModels.Index;
using Microsoft.UI.Xaml;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class AllocationTraceViewModel(AllocationStepService stepService) : ObservableObject
{
    private const int RunStepDelayMs = 150;

    private AllocationStepService StepService { get; } = stepService;

    private DatabaseSource? Database { get; set; }

    public AllocationUnit? AllocationUnit { get; private set; }

    private DateTime? QueryTime { get; set; }

    [ObservableProperty]
    private PlanNode? _planNode;

    [ObservableProperty]
    private ScanModeResult? _scanMode;

    [ObservableProperty]
    private ObservableCollection<AccessStep> _stepHistory = [];

    [ObservableProperty]
    private ObservableCollection<IndexRecordModel> _resultRecords = [];

    [ObservableProperty]
    private AccessStep? _currentStep;

    [ObservableProperty]
    private AccessStrategy? _strategy;

    [ObservableProperty]
    private bool _isStepping;

    [ObservableProperty]
    private bool _isStepComplete;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isRunningToEnd;

    [ObservableProperty]
    private bool _isTraceVisible;

    public event EventHandler<PageNavigatedEventArgs>? PageNavigated;

    private bool _hasNavigatedSinceReset;

    public AccessPhase? CurrentPhase => CurrentStep?.AccessPhase;

    public AccessCounters CurrentCounters => CurrentStep?.Counters ?? default;

    public bool IsWalkInProgress => IsStepping && !IsStepComplete;

    public AccessStrategy? SeekDescription
        => AllocationUnit is null
            ? null
            : AccessStrategyBuilder.BuildAllocationScan(GetResidual(),
                                                      PlanNode?.PredicateInfo?.RowGoal,
                                                      hasUntranslatedResidual: PlanNode?.PredicateInfo?.HasUntranslatedPredicate == true);

    public GridLength BodyColumnWidth => IsTraceVisible ? new GridLength(2, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);

    public GridLength TraceColumnWidth => IsTraceVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public Visibility TraceVisibility => IsTraceVisible ? Visibility.Visible : Visibility.Collapsed;

    partial void OnCurrentStepChanged(AccessStep? value)
    {
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(CurrentCounters));
    }

    partial void OnIsSteppingChanged(bool value) => OnPropertyChanged(nameof(IsWalkInProgress));

    partial void OnIsStepCompleteChanged(bool value) => OnPropertyChanged(nameof(IsWalkInProgress));

    partial void OnIsTraceVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(BodyColumnWidth));
        OnPropertyChanged(nameof(TraceColumnWidth));
        OnPropertyChanged(nameof(TraceVisibility));
    }

    public void Prepare(DatabaseSource database,
                        AllocationUnit allocationUnit,
                        PlanNode? planNode,
                        DateTime? queryTime,
                        ScanModeResult? scanMode)
    {
        Database = database;
        AllocationUnit = allocationUnit;
        QueryTime = queryTime;

        PlanNode = planNode;
        ScanMode = scanMode;

        ResetStep();

        OnPropertyChanged(nameof(AllocationUnit));
        OnPropertyChanged(nameof(SeekDescription));
    }

    public void Clear()
    {
        ResetStep();

        PlanNode = null;
        ScanMode = null;
        QueryTime = null;

        OnPropertyChanged(nameof(SeekDescription));
    }

    [RelayCommand]
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

            await Task.Delay(RunStepDelayMs);
        }

        IsRunning = false;
    }

    [RelayCommand]
    public async Task StepNext()
    {
        if (Database is null || AllocationUnit is null)
        {
            return;
        }

        if (!IsStepping)
        {
            await StartAsync();
        }

        var step = await Task.Run(() => StepService.StepNextAsync(CancellationToken.None));

        if (step is null)
        {
            IsStepComplete = true;
            IsRunning = false;

            return;
        }

        Append(step);

        if (step is AccessStep.Row { EmittedRecord: { } emitted })
        {
            ResultRecords.Add(ToRecordModel(emitted));
        }

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
            PageNavigated?.Invoke(this, new PageNavigatedEventArgs(pageAddress, !_hasNavigatedSinceReset));

            _hasNavigatedSinceReset = true;
        }

        if (step is AccessStep.Stopped)
        {
            IsStepComplete = true;
            IsRunning = false;
        }
    }

    [RelayCommand]
    public async Task RunToEnd()
    {
        if (Database is null || AllocationUnit is null)
        {
            return;
        }

        IsRunningToEnd = true;

        try
        {
            if (!IsStepping)
            {
                await StartAsync();
            }

            await Task.Run(async () =>
            {
                while (await StepService.StepNextAsync(CancellationToken.None) is not null)
                {
                }
            });

            var steps = new ObservableCollection<AccessStep>();

            var results = new ObservableCollection<IndexRecordModel>();

            foreach (var step in StepService.History)
            {
                Append(step, steps);

                if (step is AccessStep.Row { EmittedRecord: { } emitted })
                {
                    results.Add(ToRecordModel(emitted));
                }
            }

            StepHistory = steps;
            ResultRecords = results;
            CurrentStep = StepService.Current;
            IsStepComplete = true;
        }
        finally
        {
            IsRunningToEnd = false;
        }
    }

    [RelayCommand]
    public void ResetStep()
    {
        IsRunning = false;
        IsRunningToEnd = false;
        IsStepping = false;
        IsStepComplete = false;

        _hasNavigatedSinceReset = false;

        StepHistory = [];
        ResultRecords = [];
        CurrentStep = null;
        Strategy = null;
    }

    private static IndexRecordModel ToRecordModel(IRecord record)
    {
        return new IndexRecordModel
        {
            Slot = record.Slot,
            Fields =
            [
                .. record.Fields.Select(f => new IndexRecordFieldModel
                {
                    Name = f.Name,
                    Value = f.Value,
                    DataType = f.ColumnStructure.DataType
                })
            ]
        };
    }

    private async Task StartAsync()
    {
        var evaluationContext = QueryTime is { } queryTime ? new EvaluationContext(queryTime) : null;

        await Task.Run(() => StepService.StartAsync(Database!,
                                                    AllocationUnit!.FirstIamPage,
                                                    GetResidual(),
                                                    CancellationToken.None,
                                                    PlanNode?.PredicateInfo?.RowGoal,
                                                    evaluationContext,
                                                    PlanNode?.PredicateInfo?.HasUntranslatedPredicate == true));

        Strategy = StepService.Strategy;
        IsStepping = true;
    }

    private AccessPredicate? GetResidual()
    {
        return PlanNode?.PredicateInfo?.Residual;
    }

    private void Append(AccessStep step)
    {
        Append(step, StepHistory);
    }

    private const int HistoryLimit = 1000;

    private static void Append(AccessStep step, ObservableCollection<AccessStep> history)
    {
        if (step is AccessStep.Row row && history.Count > 0)
        {
            var latest = history[0];

            if (latest is AccessStep.Row previous && previous.Outcome == row.Outcome && Math.Abs(row.Slot - previous.Slot) == 1)
            {
                history[0] = new AccessStep.RowRun(previous.Slot, row.Slot, row.Outcome)
                {
                    Count = 2,
                    HasResidual = row.HasResidual,
                    HasRange = row.HasRange,
                    EmitCount = EmitOf(previous) + EmitOf(row),
                    Counters = row.Counters
                };

                return;
            }

            if (latest is AccessStep.RowRun run && run.Outcome == row.Outcome && Math.Abs(row.Slot - run.ToSlot) == 1)
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
}
