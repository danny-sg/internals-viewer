using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Pages;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Records.Index;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;

namespace InternalsViewer.Execution.Iterators.DataAccess;

/// <summary>
/// Fetches a single heap row from its row identifier, the access path a RID lookup uses
/// </summary>
/// <remarks>
/// A heap has no tree to descend, so the row identifier names the page and slot outright and the fetch is one page read. The exception is
/// a forwarded row, where the slot holds a stub pointing at the page the row moved to, costing a second read.
/// </remarks>
public sealed class HeapFetchIterator(IPageService pageService, IRecordService recordService) : IteratorBase
{
    private readonly byte[] _pageBuffer = new byte[PageData.Size];

    private PageAddress? _currentPageAddress;

    public override PageAddress? CurrentPageAddress => _currentPageAddress;

    public override AccessStrategy? Strategy => CurrentStrategy;

    public int OpenCount { get; private set; }

    private AccessStrategy? CurrentStrategy { get; set; }

    private AccessPredicate? Residual { get; set; }

    private RowIdentifier? Target { get; set; }

    private AccessStep? PendingRebind { get; set; }

    private bool IsFetched { get; set; }

    private IRecord? FetchedRecord { get; set; }

    private AccessCounters Counters { get; set; }

    public override async Task OpenAsync(IteratorDefinition definition, IteratorContext context,
        CancellationToken cancellationToken)
    {
        var fetch = definition.Expect<HeapFetchDefinition>();

        await PrepareAsync(definition, context, cancellationToken);

        OpenCount++;

        Residual = fetch.Residual;
        IsFetched = false;
        FetchedRecord = null;
        PendingRebind = null;

        if (context.CorrelatedRow is { } outerRecord)
        {
            Target = GetRowIdentifier(outerRecord);

            PendingRebind = new AccessStep.Rebind(OpenCount, default) { RowIdentifier = Target };
        }
        else
        {
            Target = fetch.RowIdentifier;
        }

        CurrentStrategy = AccessStrategyBuilder.BuildHeapFetch(fetch.Residual);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        if (PendingRebind is { } rebind)
        {
            PendingRebind = null;

            await EmitAsync(rebind with { Counters = Counters }, cancellationToken);
        }

        if (IsFetched)
        {
            await EmitAsync(new AccessStep.Stopped(AccessPaths.Results.StopReason.RangeEnded) { Counters = Counters }, cancellationToken);

            CurrentRow = null;

            return null;
        }

        await FetchAsync(cancellationToken);

        IsFetched = true;

        if (FetchedRecord is { } record)
        {
            CurrentRow = ProjectedRecord.Project(record, OutputList);

            return CurrentRow;
        }

        if (!IsComplete)
        {
            await EmitAsync(new AccessStep.Stopped(AccessPaths.Results.StopReason.RangeEnded) { Counters = Counters }, cancellationToken);
        }

        CurrentRow = null;

        return null;
    }

    private async Task FetchAsync(CancellationToken cancellationToken)
    {
        var target = Target
                     ?? throw new InvalidOperationException("A heap fetch has no row identifier, from its definition or a correlated "
                                                            + "outer row");

        // A forwarded row is only ever one hop away, because the stub is updated rather than chained
        for (var hop = 0; hop < 2; hop++)
        {
            var page = await ReadPageAsync(target.PageAddress, cancellationToken);

            var accessor = new HeapPageAccessor(page, recordService);

            _currentPageAddress = accessor.PageAddress;

            Counters = Counters.AddPageRead();

            await EmitAsync(new AccessStep.ReadPage(accessor.PageAddress, 0, false, true, accessor.SlotCount)
                            {
                                IsHeap = true,
                                Counters = Counters
                            }, 
                            cancellationToken);

            if (target.SlotId >= accessor.SlotCount)
            {
                return;
            }

            var record = accessor.GetRecord(target.SlotId);

            if (GetForwardingTarget(record) is { } forwardedTo)
            {
                await EmitAsync(new AccessStep.ForwardedRecord(target, forwardedTo) { Counters = Counters }, cancellationToken);

                target = forwardedTo;

                continue;
            }

            await ReadAsync(accessor, target.SlotId, record, cancellationToken);

            return;
        }
    }

    private async Task ReadAsync(HeapPageAccessor accessor, int slot, IRecord record, CancellationToken cancellationToken)
    {
        var hasResidual = Residual is not (null or AccessPredicate.True or AccessPredicate.NoTranslation);

        if (record.IsGhost)
        {
            Counters = Counters.AddGhostSkipped();

            await EmitAsync(new AccessStep.Row(slot, RowOutcome.Ghost)
                            {
                                HasResidual = hasResidual,
                                HasRange = false,
                                Counters = Counters
                            }, 
                            cancellationToken);

            return;
        }

        Counters = Counters.AddRowRead();

        var outcome = Evaluate(record) switch
        {
            true => RowOutcome.Match,
            false => RowOutcome.NoMatch,
            _ => RowOutcome.Unknown
        };

        if (outcome == RowOutcome.Match)
        {
            Counters = Counters.AddRowOutput();

            FetchedRecord = RecordSnapshot.Detach(record);
        }

        await EmitAsync(new AccessStep.Row(slot, outcome)
                        {
                            HasResidual = hasResidual,
                            HasRange = false,
                            IsFetched = true,
                            EmittedRecord = FetchedRecord,
                            Counters = Counters
                        }, 
                        cancellationToken);
    }

    private static RowIdentifier GetRowIdentifier(IRecord outerRecord)
    {
        var record = ProjectedRecord.Unwrap(outerRecord);

        if (record is FixedVarIndexRecord { Rid: { } rid })
        {
            return rid;
        }

        if (record is CdIndexRecord { Rid: { } compressedRid })
        {
            return compressedRid;
        }

        throw new InvalidOperationException("The outer row carries no row identifier, so it cannot drive a RID lookup");
    }

    /// <summary>
    /// The page and slot a stub points at, or null when the row is really here
    /// </summary>
    private static RowIdentifier? GetForwardingTarget(IRecord record)
        => record switch
        {
            DataRecord { RecordType: RecordType.ForwardingStub } data
                => data.ForwardingStub,
            CdRecord { RecordType: CompressedRecordType.Forwarding } compressed
                => compressed.RowIdentifier,
            _ => null
        };

    private bool? Evaluate(IRecord record)
    {
        if (Residual is null or AccessPredicate.True or AccessPredicate.NoTranslation)
        {
            return true;
        }

        return PredicateEvaluator.Evaluate(Residual, new RecordRowValueSource(record), Context.EvaluationContext);
    }

    private async Task<DataPage> ReadPageAsync(PageAddress pageAddress, CancellationToken cancellationToken)
    {
        var page = await pageService.GetPage(Context.Database, pageAddress, _pageBuffer, cancellationToken);

        return page as DataPage
               ?? throw new InvalidOperationException($"Page {pageAddress} is not a data page, so it holds no heap row");
    }
}
