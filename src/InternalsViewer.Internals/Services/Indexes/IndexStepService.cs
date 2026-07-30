using System.Threading;
using InternalsViewer.Internals.DataAccess.AccessPaths;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.Executors;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.DataAccess;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Internals.Services.Indexes;

/// <summary>
/// Drives a seek across page boundaries, loading pages as the walk descends or follows leaf links
/// </summary>
public sealed class IndexStepService(IPageService pageService, IRecordService recordService)
{
    private readonly byte[] _pageBuffer = new byte[PageData.Size];

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; private set; }

    public PageAddress? CurrentPageAddress => CurrentPage?.PageAddress;

    public AccessStrategy? Strategy { get; private set; }

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    private DatabaseSource Database { get; set; } = null!;

    private IndexStructure IndexStructure { get; set; } = null!;

    private SeekBounds Bounds { get; set; } = SeekBounds.All;

    private IReadOnlyList<SeekBounds> Ranges { get; set; } = [];

    private int RangeIndex { get; set; }

    private PageAddress RootPage { get; set; }

    private ScanDirection Direction { get; set; } = ScanDirection.Forward;

    private AccessPredicate? Residual { get; set; }

    private EvaluationContext EvaluationContext { get; set; } = EvaluationContext.Now;

    private long? RowGoal { get; set; }

    private long? PlanRowGoal { get; set; }

    private IIndexPageAccessor CurrentPage { get; set; } = null!;

    private IEnumerator<AccessStep> CurrentPageSteps { get; set; } = null!;

    private AccessCounters Counters { get; set; }

    private List<AccessStep> TakenSteps { get; } = [];

    public async Task StartAsync(DatabaseSource database,
                                 long allocationUnitId,
                                 PageAddress rootPageAddress,
                                 IReadOnlyList<SeekBounds> ranges,
                                 AccessPredicate? residual,
                                 ScanDirection direction,
                                 CancellationToken cancellationToken,
                                 long? rowGoal = null,
                                 bool hasUntranslatedResidual = false,
                                 EvaluationContext? evaluationContext = null)
    {
        Database = database;
        EvaluationContext = evaluationContext ?? EvaluationContext.Now;
        IndexStructure = IndexStructureProvider.GetIndexStructure(database, allocationUnitId);

        if (IndexStructure.IndexKeyColumns.Count > 0 && IndexStructure.IndexKeyColumns[0].IsDescending)
        {
            ranges = [.. ranges.Select(r => r.Reversed())];
        }

        var bounds = ranges.Count > 0 ? ranges[0] : SeekBounds.All;

        Ranges = ranges;
        RangeIndex = 0;
        RootPage = rootPageAddress;
        Bounds = bounds;
        Residual = residual;
        PlanRowGoal = rowGoal;

        var uniqueGoal = GetRowGoal(IndexStructure, bounds);

        string? rowGoalReason = null;

        if (uniqueGoal is not null && (rowGoal is null || uniqueGoal < rowGoal))
        {
            RowGoal = uniqueGoal;

            rowGoalReason = "The index is unique and the seek fixes every key column with an equality, so at most one row can match. " +
                            "The walk stops after the first match instead of reading on to check.";
        }
        else if (rowGoal is not null)
        {
            RowGoal = rowGoal;

            rowGoalReason = $"A TOP above this operator stops requesting rows once {rowGoal:N0} have been returned, " +
                            "so the walk ends after that many rows have been output.";
        }
        else
        {
            RowGoal = null;
        }

        Strategy = AccessStrategyBuilder.Build(IndexStructure,
                                             bounds,
                                             direction,
                                             RowGoal,
                                             residual,
                                             rowGoalReason,
                                             Ranges,
                                             hasUntranslatedResidual);
        Direction = direction;
        Counters = default(AccessCounters).AddRangeSeek();
        IsComplete = false;

        TakenSteps.Clear();

        await LoadPageAsync(rootPageAddress, cancellationToken);
    }

    public async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        if (!CurrentPageSteps.MoveNext())
        {
            IsComplete = true;

            return null;
        }

        var step = CurrentPageSteps.Current;

        TakenSteps.Add(step);
        Counters = step.Counters;

        if (step is AccessStep.Descend(_, var childPage))
        {
            await LoadPageAsync(childPage, cancellationToken);

            return step;
        }

        if (step is AccessStep.Stopped(StopReason.PageExhausted) && CurrentPage.IsLeaf)
        {
            var nextPage = Direction == ScanDirection.Forward ? CurrentPage.NextPage : CurrentPage.PreviousPage;

            if (nextPage != PageAddress.Empty)
            {
                Counters = Counters.AddLeafLinkFollowed();

                var link = new AccessStep.LeafLink(CurrentPage.PageAddress, nextPage)
                {
                    Direction = Direction,
                    Counters = Counters
                };

                TakenSteps.Add(link);

                await LoadPageAsync(nextPage, cancellationToken, isContinuation: true);

                IsComplete = false;

                return link;
            }
        }

        if (step is AccessStep.Stopped(var reason) && HasNextRange(reason))
        {
            return await ReseekAsync(cancellationToken);
        }

        if (step is AccessStep.Stopped)
        {
            IsComplete = true;
        }

        return step;
    }

    private bool HasNextRange(StopReason reason)
    {
        if (RangeIndex + 1 >= Ranges.Count)
        {
            return false;
        }

        return reason == StopReason.RangeEnded
               || reason == StopReason.PageExhausted
               || (reason == StopReason.RowGoalMet && RowGoal != PlanRowGoal);
    }

    private async Task<AccessStep> ReseekAsync(CancellationToken cancellationToken)
    {
        RangeIndex++;
        Bounds = Ranges[RangeIndex];

        var uniqueGoal = GetRowGoal(IndexStructure, Bounds);

        if (uniqueGoal is not null)
        {
            var target = Counters.RowsOutput + uniqueGoal.Value;

            RowGoal = PlanRowGoal is { } plan && plan < target ? plan : target;
        }
        else
        {
            RowGoal = PlanRowGoal;
        }

        Counters = Counters.AddRangeSeek();

        var reseek = new AccessStep.Reseek(RangeIndex + 1, Ranges.Count)
        {
            Bounds = Bounds,
            Counters = Counters
        };

        TakenSteps.Add(reseek);

        await LoadPageAsync(RootPage, cancellationToken);

        return reseek;
    }

    private async Task LoadPageAsync(PageAddress pageAddress, CancellationToken cancellationToken, bool isContinuation = false)
    {
        var page = await PageService.GetPage(Database, pageAddress, _pageBuffer, cancellationToken);

        CurrentPage = page switch
        {
            IndexPage indexPage
                => new IndexPageAccessor(indexPage, RecordService, IndexStructure),
            DataPage dataPage
                => new ClusteredLeafPageAccessor(dataPage, RecordService, IndexStructure),
            _ =>
                throw new InvalidOperationException($"Unexpected page type {page.GetType()} at {pageAddress}")
        };

        CurrentPageSteps = IndexSeekExecutor.Execute(CurrentPage, 
                                                    Bounds, 
                                                    Direction, 
                                                    Residual, 
                                                    RowGoal, 
                                                    isContinuation, 
                                                    counters: Counters,
                                                    evaluationContext: EvaluationContext)
                                           .GetEnumerator();
    }

    private static long? GetRowGoal(IndexStructure indexStructure, SeekBounds bounds)
    {
        var isUniqueEquality = indexStructure.IsUnique
                               && bounds is { HasStart: true, HasEnd: true, IsStartInclusive: true, IsEndInclusive: true }
                               && bounds.StartValue.Equals(bounds.EndValue)
                               && bounds.CompareWidth >= indexStructure.IndexKeyColumns.Count;

        return isUniqueEquality ? 1 : null;
    }
}