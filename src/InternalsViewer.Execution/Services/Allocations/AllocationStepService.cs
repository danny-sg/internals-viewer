using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Executors;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Pages;
using InternalsViewer.Execution.Pages;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;

namespace InternalsViewer.Execution.Services.Allocations;

/// <summary>
/// Drives an allocation order scan, following the IAM chain and reading allocated pages
/// </summary>
public sealed class AllocationStepService(IPageService pageService, IRecordService recordService) : IStepService
{
    private readonly byte[] _pageBuffer = new byte[PageData.Size];

    private readonly Queue<AccessStep> _pending = new();

    private int _slotIndex;

    private bool _visitedSlot;

    private bool _visitedExtent;

    private int _extentIndex;

    private int _pageInExtent;

    private short _pfsFileId;

    private int _pfsInterval;

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; private set; }

    public PageAddress? CurrentPageAddress => CurrentPage?.PageAddress;

    public AccessStrategy? Strategy { get; private set; }

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    private DatabaseSource Database { get; set; } = null!;

    private AccessPredicate? Residual { get; set; }

    private long? RowGoal { get; set; }

    private EvaluationContext EvaluationContext { get; set; } = EvaluationContext.Now;

    private IamPage? CurrentIam { get; set; }

    private IRowPageAccessor? CurrentPage { get; set; }

    private IEnumerator<AccessStep>? CurrentPageSteps { get; set; }

    private AccessCounters Counters { get; set; }

    private List<AccessStep> TakenSteps { get; } = [];

    public async Task StartAsync(DatabaseSource database,
                                 PageAddress firstIamPage,
                                 AccessPredicate? residual,
                                 CancellationToken cancellationToken,
                                 long? rowGoal = null,
                                 EvaluationContext? evaluationContext = null,
                                 bool hasUntranslatedResidual = false)
    {
        Database = database;
        Residual = residual;
        RowGoal = rowGoal;
        EvaluationContext = evaluationContext ?? EvaluationContext.Now;

        var rowGoalReason = rowGoal is { } goal
            ? $"A TOP above this operator stops requesting rows once {goal:N0} have been returned, " +
              "so the scan ends after that many rows have been output."
            : null;

        Strategy = AccessStrategyBuilder.BuildAllocationScan(residual, rowGoal, rowGoalReason, hasUntranslatedResidual) with
        {
            EntryPoint = firstIamPage,
            EntryPointSource = "sys.sysallocunits.pgfirstiam"
        };

        Counters = default;
        IsComplete = false;
        CurrentPage = null;
        CurrentPageSteps = null;

        _pfsFileId = -1;
        _pfsInterval = -1;

        _pending.Clear();
        TakenSteps.Clear();

        await LoadIamAsync(firstIamPage, cancellationToken);
    }

    public async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_pending.Count > 0)
            {
                return Take(_pending.Dequeue());
            }

            if (CurrentPageSteps is not null)
            {
                if (CurrentPageSteps.MoveNext())
                {
                    return Take(CurrentPageSteps.Current);
                }

                CurrentPageSteps = null;
                CurrentPage = null;
            }

            await AdvanceAsync(cancellationToken);
        }
    }

    private AccessStep Take(AccessStep step)
    {
        TakenSteps.Add(step);

        Counters = step.Counters;

        if (step is AccessStep.Stopped)
        {
            IsComplete = true;
        }

        return step;
    }

    private async Task AdvanceAsync(CancellationToken cancellationToken)
    {
        var iam = CurrentIam!;

        while (_slotIndex < iam.SinglePageSlots.Length)
        {
            var slot = iam.SinglePageSlots[_slotIndex];

            _slotIndex++;

            if (slot == PageAddress.Empty)
            {
                continue;
            }

            _visitedSlot = true;

            _pending.Enqueue(new AccessStep.Advance($"Single page slot {_slotIndex - 1}, allocated from a mixed extent")
            {
                Counters = Counters
            });

            await VisitPageAsync(slot, cancellationToken);

            return;
        }

        if (_visitedSlot)
        {
            _visitedSlot = false;

            _pending.Enqueue(new AccessStep.Advance("Single page slots complete, moving to the allocated extents")
            {
                Counters = Counters
            });

            return;
        }

        while (_extentIndex < AllocationPage.AllocationExtentInterval)
        {
            if (_pageInExtent < 0)
            {
                if (!IsExtentAllocated(iam, _extentIndex))
                {
                    _extentIndex++;

                    continue;
                }

                _pageInExtent = 0;

                if (_visitedExtent)
                {
                    _pending.Enqueue(new AccessStep.Advance("Extent complete, moving to the next allocated extent")
                    {
                        Counters = Counters
                    });
                }

                _visitedExtent = true;

                Counters = Counters.AddExtentVisited();

                _pending.Enqueue(new AccessStep.ExtentStart(GetExtentPage(iam, _extentIndex, 0), _extentIndex)
                {
                    Counters = Counters
                });

                return;
            }

            if (_pageInExtent < 8)
            {
                var address = GetExtentPage(iam, _extentIndex, _pageInExtent);

                if (_pageInExtent > 0)
                {
                    _pending.Enqueue(new AccessStep.Advance($"Next page in extent ({_pageInExtent + 1} of 8)")
                    {
                        Counters = Counters
                    });
                }

                _pageInExtent++;

                await VisitPageAsync(address, cancellationToken);

                return;
            }

            _pageInExtent = -1;
            _extentIndex++;
        }

        var next = iam.PageHeader.NextPage;

        if (next == PageAddress.Empty)
        {
            _pending.Enqueue(new AccessStep.Stopped(StopReason.AllocationExhausted) { Counters = Counters });

            return;
        }

        _pending.Enqueue(new AccessStep.IamLink(iam.PageAddress, next) { Counters = Counters });

        await LoadIamAsync(next, cancellationToken);
    }

    private async Task VisitPageAsync(PageAddress address, CancellationToken cancellationToken)
    {
        var interval = address.PageId / PfsPage.PfsInterval;

        if (address.FileId != _pfsFileId || interval != _pfsInterval)
        {
            _pfsFileId = address.FileId;
            _pfsInterval = interval;

            Counters = Counters.AddPfsPageRead();

            var pfsPageId = interval == 0 ? 1 : interval * PfsPage.PfsInterval;

            _pending.Enqueue(new AccessStep.PfsRead(new PageAddress(address.FileId, pfsPageId), interval * PfsPage.PfsInterval)
            {
                Counters = Counters
            });
        }

        var status = Database.Pfs.TryGetValue(address.FileId, out var pfs) ? pfs.GetPageStatus(address.PageId) : PfsByte.Unknown;

        _pending.Enqueue(new AccessStep.PfsCheck(address, status.IsAllocated)
        {
            Status = status.ToString().Replace("PFS Status: ", string.Empty),
            Counters = Counters
        });

        if (!status.IsAllocated)
        {
            Skip(address, PageSkipReason.NotAllocated);

            return;
        }

        var page = await PageService.GetPage(Database, address, _pageBuffer, cancellationToken);

        switch (page)
        {
            case DataPage dataPage:
                CurrentPage = new HeapPageAccessor(dataPage, RecordService);

                CurrentPageSteps = AllocationScanExecutor.Execute(CurrentPage,
                                                                  Residual,
                                                                  RowGoal,
                                                                  Counters,
                                                                  evaluationContext: EvaluationContext,
                                                                  isHeap: dataPage.AllocationUnit.IndexType == IndexType.Heap)
                                                         .GetEnumerator();
                break;

            case IndexPage:
                ReadThenSkip(page, PageSkipReason.IndexPage);
                break;

            case IamPage:
                ReadThenSkip(page, PageSkipReason.IamPage);
                break;

            default:
                ReadThenSkip(page, PageSkipReason.Other);
                break;
        }
    }

    private void Skip(PageAddress address, PageSkipReason reason)
    {
        Counters = Counters.AddPageSkipped();

        _pending.Enqueue(new AccessStep.PageSkipped(address, reason) { Counters = Counters });
    }

    private void ReadThenSkip(Page page, PageSkipReason reason)
    {
        Counters = Counters.AddPageRead();

        var header = page.PageHeader;

        _pending.Enqueue(new AccessStep.ReadPage(header.PageAddress, (byte)header.Level, false, header.Level == 0, header.SlotCount)
        {
            Counters = Counters
        });

        Skip(header.PageAddress, reason);
    }

    private async Task LoadIamAsync(PageAddress address, CancellationToken cancellationToken)
    {
        var page = await PageService.GetPage(Database, address, _pageBuffer, cancellationToken);

        if (page is not IamPage iam)
        {
            throw new InvalidOperationException($"Expected an IAM page at {address}, found {page.GetType().Name}");
        }

        CurrentIam = iam;

        _slotIndex = 0;
        _visitedSlot = false;
        _visitedExtent = false;
        _extentIndex = 0;
        _pageInExtent = -1;

        Counters = Counters.AddIamPageRead();

        var extentCount = CountAllocatedExtents(iam);

        var singlePageCount = iam.SinglePageSlots.Count(s => s != PageAddress.Empty);

        _pending.Enqueue(new AccessStep.IamRead(address, extentCount, singlePageCount) { Counters = Counters });
    }

    private static PageAddress GetExtentPage(IamPage iam, int extent, int pageInExtent)
    {
        return new PageAddress(iam.StartPage.FileId, iam.StartPage.PageId + extent * 8 + pageInExtent);
    }

    private static bool IsExtentAllocated(IamPage iam, int extent)
    {
        return ((iam.AllocationMap[extent >> 3] >> (extent & 7)) & 1) != 0;
    }

    private static int CountAllocatedExtents(IamPage iam)
    {
        var count = 0;

        foreach (var value in iam.AllocationMap)
        {
            count += System.Numerics.BitOperations.PopCount(value);
        }

        return count;
    }
}
