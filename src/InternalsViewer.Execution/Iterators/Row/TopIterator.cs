using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Row;

/// <summary>
/// Stops reading its input once the requested number of rows has passed through
/// </summary>
/// <remarks>
/// A TOP produces no rows of its own, it only counts what its input hands up and closes it when the count is reached. Steps from below are
/// passed on with the identity they arrived with, so the reads still belong to whatever performed them, and this operator adds only the
/// count and the point the walk was stopped.
/// </remarks>
public sealed class TopIterator(IIteratorFactory factory) : IUnaryStepIterator
{
    public int IteratorId { get; set; }

    public IReadOnlyList<AccessStep> History => TakenSteps;

    public AccessStep? Current => TakenSteps.Count == 0 ? null : TakenSteps[^1];

    public bool IsComplete { get; private set; }

    public PageAddress? CurrentPageAddress => Input?.CurrentPageAddress;

    public AccessStrategy? Strategy => Input?.Strategy;

    /// <summary>
    /// Rows counted so far, which is what the limit is tested against
    /// </summary>
    public long RowCount { get; private set; }

    public IStepIterator? Input { get; private set; }

    private long Limit { get; set; }

    private bool PendingStart { get; set; }

    private Queue<AccessStep> Pending { get; } = new();

    private List<AccessStep> TakenSteps { get; } = [];

    public async Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
    {
        var top = definition.Expect<TopDefinition>();

        if (top.IsPercent)
        {
            throw new ArgumentException("A percentage TOP is not simulated, because the input has to be counted before the limit is known");
        }

        if (Input is not null)
        {
            await CloseAsync();
        }

        Input = factory.Create(top.Source);
        Limit = top.RowCount;
        RowCount = 0;
        IsComplete = false;
        PendingStart = true;

        Pending.Clear();
        TakenSteps.Clear();

        await Input.OpenAsync(context, top.Source, cancellationToken);
    }

    public async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        if (IsComplete)
        {
            return null;
        }

        if (PendingStart)
        {
            PendingStart = false;

            return Take(new AccessStep.TopStart(Limit) { Source = IteratorId });
        }

        if (Pending.Count > 0)
        {
            return Take(Pending.Dequeue());
        }

        if (Input is null)
        {
            IsComplete = true;

            return null;
        }

        var step = await Input.StepNextAsync(cancellationToken);

        if (step is null)
        {
            IsComplete = true;

            return null;
        }

        if (Input.GetOutputRow(step) is not { } record)
        {
            return Take(step);
        }

        RowCount++;

        Pending.Enqueue(new AccessStep.TopRow(RowCount, Limit)
        {
            Source = IteratorId,
            EmittedRecord = record,
            Counters = step.Counters
        });

        if (RowCount >= Limit)
        {
            await Input.CloseAsync();

            Pending.Enqueue(new AccessStep.Stopped(StopReason.RowGoalMet)
            {
                Source = IteratorId,
                Counters = step.Counters
            });
        }

        return Take(step);
    }

    public async Task CloseAsync()
    {
        if (Input is not null)
        {
            await Input.CloseAsync();
        }

        IsComplete = true;
    }

    /// <summary>
    /// A row this TOP let through, which is its input's row counted and passed on
    /// </summary>
    public IRecord? GetOutputRow(AccessStep step)
        => step.Source == IteratorId && step is AccessStep.TopRow { EmittedRecord: { } record } ? record : null;

    private AccessStep Take(AccessStep step)
    {
        TakenSteps.Add(step);

        if (step is AccessStep.Stopped)
        {
            IsComplete = true;
        }

        return step;
    }
}
