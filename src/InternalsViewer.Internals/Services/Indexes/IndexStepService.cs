using System.Threading;
using InternalsViewer.Internals.DataAccess.AccessPaths;
using InternalsViewer.Internals.DataAccess.AccessPaths.Binding;
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
/// <remarks>
/// <see cref="PageSeekExecutor"/> only understands a single already-loaded page, and page loading is
/// asynchronous, so this service owns the async orchestration that a synchronous
/// <see cref="AccessPathStepper"/> cannot provide on its own.
/// </remarks>
public sealed class IndexStepService(IPageService pageService, IRecordService recordService)
{
    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    private DatabaseSource Database { get; set; } = null!;

    private IndexStructure IndexStructure { get; set; } = null!;

    private SeekBounds Bounds { get; set; } = SeekBounds.All;

    private ScanDirection Direction { get; set; } = ScanDirection.Forward;

    private AccessPredicate? Residual { get; set; }

    private long? RowGoal { get; set; }

    private IIndexAccessPage CurrentPage { get; set; } = null!;

    private IEnumerator<AccessStep> CurrentPageSteps { get; set; } = null!;

    private AccessCounters Counters { get; set; }

    private List<AccessStep> TakenSteps { get; } = [];

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; private set; }

    public PageAddress? CurrentPageAddress => CurrentPage?.PageAddress;

    public SeekStrategy? Strategy { get; private set; }

    public async Task StartAsync(DatabaseSource database,
                                 long allocationUnitId,
                                 PageAddress rootPageAddress,
                                 SeekBounds bounds,
                                 AccessPredicate? residual,
                                 ScanDirection direction,
                                 CancellationToken cancellationToken)
    {
        Database = database;
        IndexStructure = IndexStructureProvider.GetIndexStructure(database, allocationUnitId);
        Bounds = bounds;
        Residual = residual;
        RowGoal = GetRowGoal(IndexStructure, bounds);
        Strategy = SeekStrategyBuilder.Build(IndexStructure, bounds, direction, RowGoal, residual);
        Direction = direction;
        Counters = default;
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

        if (step is AccessStep.Stopped(StopReason.PageExhausted) &&
            CurrentPage.IsLeaf &&
            Direction == ScanDirection.Forward)
        {
            var nextPage = CurrentPage.NextPage;

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

        if (step is AccessStep.Stopped)
        {
            IsComplete = true;
        }

        return step;
    }

    private async Task LoadPageAsync(PageAddress pageAddress, CancellationToken cancellationToken, bool isContinuation = false)
    {
        var page = await PageService.GetPage(Database, pageAddress, cancellationToken);

        CurrentPage = page switch
        {
            IndexPage indexPage
                => new IndexAccessPage(indexPage, [.. RecordService.GetIndexRecords(indexPage)], IndexStructure),
            DataPage dataPage
                => new ClusteredLeafAccessPage(dataPage, [.. RecordService.GetDataRecords(dataPage)], IndexStructure),
            _ => 
                throw new InvalidOperationException($"Unexpected page type {page.GetType()} at {pageAddress}")
        };

        var executor = new PageSeekExecutor(new RecordRowBinder());

        CurrentPageSteps = executor.Execute(CurrentPage, Bounds, Direction, Residual, RowGoal, isContinuation, counters: Counters)
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