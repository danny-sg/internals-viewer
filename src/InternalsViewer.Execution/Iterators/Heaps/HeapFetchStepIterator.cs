using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Pages;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Engine.Records.CdRecordType;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;

namespace InternalsViewer.Execution.Iterators.Heaps;

/// <summary>
/// Fetches a single heap row from its row identifier, the access path a RID lookup uses
/// </summary>
/// <remarks>
/// A heap has no tree to descend, so the row identifier names the page and slot outright and the fetch is one page read. The exception is
/// a forwarded row, where the slot holds a stub pointing at the page the row moved to, costing a second read.
/// </remarks>
public sealed class HeapFetchStepIterator(IPageService pageService, IRecordService recordService) : IStepIterator
{
    private readonly byte[] _pageBuffer = new byte[PageData.Size];

    public int IteratorId { get; set; }

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; private set; }

    public PageAddress? CurrentPageAddress { get; private set; }

    public AccessStrategy? Strategy { get; private set; }

    private DatabaseSource Database { get; set; } = null!;

    private AccessPredicate? Residual { get; set; }

    private EvaluationContext EvaluationContext { get; set; } = EvaluationContext.Now;

    private AccessCounters Counters { get; set; }

    private IEnumerator<AccessStep>? Steps { get; set; }

    private List<AccessStep> TakenSteps { get; } = [];

    public Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
    {
        var fetch = definition.Expect<HeapFetchDefinition>();

        Database = context.Database;
        Residual = fetch.Residual;
        EvaluationContext = context.EvaluationContext;
        Counters = context.Counters;
        IsComplete = false;

        TakenSteps.Clear();

        Strategy = AccessStrategyBuilder.BuildHeapFetch(fetch.Residual);

        Steps = Fetch(fetch.RowIdentifier, cancellationToken).GetEnumerator();

        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        Steps?.Dispose();
        Steps = null;

        IsComplete = true;

        return Task.CompletedTask;
    }

    public Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Steps is null || !Steps.MoveNext())
        {
            IsComplete = true;

            return Task.FromResult<AccessStep?>(null);
        }

        var step = Steps.Current with { Source = IteratorId };

        TakenSteps.Add(step);

        Counters = step.Counters;

        if (step is AccessStep.Stopped)
        {
            IsComplete = true;
        }

        return Task.FromResult<AccessStep?>(step);
    }

    private IEnumerable<AccessStep> Fetch(RowIdentifier rowIdentifier, CancellationToken cancellationToken)
    {
        var target = rowIdentifier;

        // A forwarded row is only ever one hop away, because the stub is updated rather than chained
        for (var hop = 0; hop < 2; hop++)
        {
            var page = ReadPageAsync(target.PageAddress, cancellationToken).GetAwaiter().GetResult();

            var accessor = new HeapPageAccessor(page, recordService);

            CurrentPageAddress = accessor.PageAddress;

            Counters = Counters.AddPageRead();

            yield return new AccessStep.ReadPage(accessor.PageAddress, 0, false, true, accessor.SlotCount)
            {
                IsHeap = true,
                Counters = Counters
            };

            if (target.SlotId >= accessor.SlotCount)
            {
                yield return new AccessStep.Stopped(StopReason.RangeEnded) { Counters = Counters };

                yield break;
            }

            var record = accessor.GetRecord(target.SlotId);

            if (GetForwardingTarget(record) is { } forwardedTo)
            {
                yield return new AccessStep.ForwardedRecord(target, forwardedTo) { Counters = Counters };

                target = forwardedTo;

                continue;
            }

            foreach (var step in Read(accessor, target.SlotId, record))
            {
                yield return step;
            }

            yield break;
        }

        yield return new AccessStep.Stopped(StopReason.RangeEnded) { Counters = Counters };
    }

    private IEnumerable<AccessStep> Read(HeapPageAccessor accessor, int slot, IRecord record)
    {
        var hasResidual = Residual is not (null or AccessPredicate.True);

        if (record.IsGhost)
        {
            Counters = Counters.AddGhostSkipped();

            yield return new AccessStep.Row(slot, RowOutcome.Ghost) { HasResidual = hasResidual, HasRange = false, Counters = Counters };

            yield return new AccessStep.Stopped(StopReason.RangeEnded) { Counters = Counters };

            yield break;
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
        }

        yield return new AccessStep.Row(slot, outcome)
        {
            HasResidual = hasResidual,
            HasRange = false,
            IsFetched = true,
            EmittedRecord = outcome == RowOutcome.Match ? RecordSnapshot.Detach(record) : null,
            Counters = Counters
        };

        yield return new AccessStep.Stopped(StopReason.RangeEnded) { Counters = Counters };
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
        if (Residual is null or AccessPredicate.True)
        {
            return true;
        }

        return PredicateEvaluator.Evaluate(Residual, new RecordRowValueSource(record), EvaluationContext);
    }

    private async Task<DataPage> ReadPageAsync(PageAddress pageAddress, CancellationToken cancellationToken)
    {
        var page = await pageService.GetPage(Database, pageAddress, _pageBuffer, cancellationToken);

        return page as DataPage
               ?? throw new InvalidOperationException($"Page {pageAddress} is not a data page, so it holds no heap row");
    }
}
