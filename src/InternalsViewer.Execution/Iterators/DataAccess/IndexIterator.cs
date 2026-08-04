using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Executors;
using InternalsViewer.Execution.Interfaces.Pages;
using InternalsViewer.Execution.Pages;
using InternalsViewer.Execution.Records;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Providers.Metadata;

namespace InternalsViewer.Execution.Iterators.DataAccess;

/// <summary>
/// Drives a seek across page boundaries, loading pages as the walk descends or follows leaf links
/// </summary>
public sealed class IndexIterator(IPageService pageService, IRecordService recordService) : IteratorBase
{
    private readonly byte[] _pageBuffer = new byte[PageData.Size];

    public override PageAddress? CurrentPageAddress => CurrentPage?.PageAddress;

    public override AccessStrategy? Strategy => CurrentStrategy;

    public int OpenCount { get; private set; }

    private AccessStrategy? CurrentStrategy { get; set; }

    private IndexStructure IndexStructure { get; set; } = null!;

    private SeekBounds Bounds { get; set; } = SeekBounds.All;

    private IReadOnlyList<SeekBounds> Ranges { get; set; } = [];

    private int RangeIndex { get; set; }

    private PageAddress RootPage { get; set; }

    private ScanDirection Direction { get; set; } = ScanDirection.Forward;

    private AccessPredicate? Residual { get; set; }

    private long? RowGoal { get; set; }

    private long? PlanRowGoal { get; set; }

    private bool IsResidualChecked { get; set; }

    private AccessStep? PendingRebind { get; set; }

    private IIndexPageAccessor? CurrentPage { get; set; }

    private IEnumerator<AccessStep>? CurrentPageSteps { get; set; }

    private AccessCounters Counters { get; set; }

    public override async Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
    {
        CurrentPageSteps?.Dispose();
        CurrentPageSteps = null;

        await PrepareAsync(context, definition, cancellationToken);

        OpenCount++;

        var range = definition is SeekDefinition seek ? Resolve(seek, context) : definition.Expect<RangeDefinition>();

        var rowGoal = range.RowGoal;

        IndexStructure = IndexStructureProvider.GetIndexStructure(context.Database, range.AllocationUnitId);

        if (definition is SeekDefinition correlated)
        {
            CheckResidual(correlated.Residual, context.CorrelatedRow!);
        }

        var ranges = range.Ranges;

        if (IndexStructure.IndexKeyColumns.Count > 0 && IndexStructure.IndexKeyColumns[0].IsDescending)
        {
            ranges = [.. ranges.Select(r => r.Reversed())];
        }

        var bounds = ranges.Count > 0 ? ranges[0] : SeekBounds.All;

        Ranges = ranges;
        RangeIndex = 0;
        RootPage = range.RootPage;
        Bounds = bounds;
        Residual = range.Residual;

        var baseRows = Counters.RowsOutput;

        PlanRowGoal = rowGoal is { } planGoal ? baseRows + planGoal : null;

        var uniqueGoal = GetRowGoal(IndexStructure, bounds);

        string? rowGoalReason = null;

        if (uniqueGoal is not null && (rowGoal is null || uniqueGoal < rowGoal))
        {
            RowGoal = baseRows + uniqueGoal;

            rowGoalReason = "The index is unique and the seek fixes every key column with an equality, so at most one row can match. " +
                            "The walk stops after the first match instead of reading on to check.";
        }
        else if (rowGoal is not null)
        {
            RowGoal = PlanRowGoal;

            rowGoalReason = $"A TOP above this operator stops requesting rows once {rowGoal:N0} have been returned, " +
                            "so the walk ends after that many rows have been output.";
        }
        else
        {
            RowGoal = null;
        }

        CurrentStrategy = AccessStrategyBuilder.Build(IndexStructure,
                                                      bounds,
                                                      range.Direction,
                                                      RowGoal is { } goal ? goal - baseRows : null,
                                                      range.Residual,
                                                      rowGoalReason,
                                                      Ranges) with
        {
            EntryPoint = range.RootPage,
            EntryPointSource = "sys.sysallocunits.pgroot"
        };

        Direction = range.Direction;
        Counters = Counters.AddRangeSeek();

        await LoadPageAsync(range.RootPage, cancellationToken);
    }

    public override async Task<IRecord?> GetRowAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || CurrentPageSteps is null)
        {
            return null;
        }

        if (PendingRebind is { } rebind)
        {
            PendingRebind = null;

            await EmitAsync(rebind with { Counters = Counters }, cancellationToken);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!CurrentPageSteps.MoveNext())
            {
                IsComplete = true;
                CurrentRow = null;

                return null;
            }

            var step = CurrentPageSteps.Current;

            Counters = step.Counters;

            if (step is AccessStep.Descend(_, var childPage))
            {
                await EmitAsync(step, cancellationToken);

                await LoadPageAsync(childPage, cancellationToken);

                continue;
            }

            if (step is AccessStep.Stopped(AccessPaths.Results.StopReason.PageExhausted) && CurrentPage is { IsLeaf: true })
            {
                var nextPage = Direction == ScanDirection.Forward ? CurrentPage.NextPage : CurrentPage.PreviousPage;

                if (nextPage != PageAddress.Empty)
                {
                    Counters = Counters.AddLeafLinkFollowed();

                    await EmitAsync(new AccessStep.LeafLink(CurrentPage.PageAddress, nextPage)
                                    {
                                        Direction = Direction,
                                        Counters = Counters
                                    }, 
                                    cancellationToken);

                    await LoadPageAsync(nextPage, cancellationToken, isContinuation: true);

                    continue;
                }
            }

            if (step is AccessStep.Stopped(var reason) && HasNextRange(reason))
            {
                await ReseekAsync(cancellationToken);

                continue;
            }

            await EmitAsync(step, cancellationToken);

            if (step is AccessStep.Stopped)
            {
                CurrentRow = null;

                return null;
            }

            if (step is AccessStep.Row { EmittedRecord: { } record })
            {
                CurrentRow = ProjectedRecord.Project(record, OutputList);

                return CurrentRow;
            }
        }
    }

    public override Task CloseAsync()
    {
        CurrentPageSteps?.Dispose();
        CurrentPageSteps = null;

        return base.CloseAsync();
    }

    private RangeDefinition Resolve(SeekDefinition seek, IteratorContext context)
    {
        if (context.CorrelatedRow is not { } outerRecord)
        {
            throw new InvalidOperationException("A correlated seek can only be opened with an outer row on the context to bind from");
        }

        var source = new RecordRowValueSource(outerRecord);

        var values = new AccessValue[seek.Bindings.Count];

        for (var index = 0; index < seek.Bindings.Count; index++)
        {
            var binding = seek.Bindings[index];

            if (!outerRecord.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName,
                                                           binding.OuterColumn,
                                                           StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Outer row has no column '{binding.OuterColumn}' "
                                                    + $"to bind seek column '{binding.SeekColumn}'");
            }

            values[index] = source.GetValue(-1, binding.OuterColumn).WithColumnName(binding.SeekColumn);
        }

        var key = new AccessKey([.. values]);

        PendingRebind = new AccessStep.Rebind(OpenCount, key);

        return new RangeDefinition(seek.AllocationUnitId, seek.RootPage, [SeekBounds.Equality(key)])
        {
            NodeId = seek.NodeId,
            OutputList = seek.OutputList,
            Residual = seek.Residual,
            RowGoal = seek.RowGoal
        };
    }

    private void CheckResidual(AccessPredicate? residual, IRecord outerRecord)
    {
        if (IsResidualChecked || residual is null)
        {
            return;
        }

        IsResidualChecked = true;

        var names = IndexStructure.Columns.Select(c => c.ColumnName);

        if (IndexStructure.TableStructure is { } table)
        {
            names = names.Concat(table.Columns.Select(c => c.ColumnName));
        }

        var innerColumns = names.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (innerColumns.Count == 0)
        {
            return;
        }

        var outerColumns = outerRecord.Fields
                                      .Select(f => f.ColumnStructure.ColumnName)
                                      .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referenced = PredicateColumns.Referenced(residual)
                                         .Where(c => outerColumns.Contains(c) && !innerColumns.Contains(c))
                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                         .ToList();

        if (referenced.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException($"The residual reads {string.Join(", ", referenced.Select(c => $"'{c}'"))} from the outer "
                                            + "row, which a rebind cannot bind. Only the seek key carries outer values into the inner "
                                            + "access path, so a join predicate has to be expressed as a seek binding");
    }

    private bool HasNextRange(StopReason reason)
    {
        if (RangeIndex + 1 >= Ranges.Count)
        {
            return false;
        }

        return reason == AccessPaths.Results.StopReason.RangeEnded
               || reason == AccessPaths.Results.StopReason.PageExhausted
               || (reason == AccessPaths.Results.StopReason.RowGoalMet && RowGoal != PlanRowGoal);
    }

    private async Task ReseekAsync(CancellationToken cancellationToken)
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

        await EmitAsync(new AccessStep.Reseek(RangeIndex + 1, Ranges.Count)
                        {
                            Bounds = Bounds,
                            Counters = Counters
                        }, 
                        cancellationToken);

        await LoadPageAsync(RootPage, cancellationToken);
    }

    private async Task LoadPageAsync(PageAddress pageAddress, CancellationToken cancellationToken, bool isContinuation = false)
    {
        CurrentPageSteps?.Dispose();

        var page = await pageService.GetPage(Context.Database, pageAddress, _pageBuffer, cancellationToken);

        CurrentPage = page switch
        {
            IndexPage indexPage
                => new IndexPageAccessor(indexPage, recordService, IndexStructure),
            DataPage dataPage
                => new ClusteredLeafPageAccessor(dataPage, recordService, IndexStructure),
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
                                                     evaluationContext: Context.EvaluationContext)
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
