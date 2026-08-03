using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Joins.Inputs;

/// <summary>
/// Pulls one row at a time from an input, reporting everything the input did on the way
/// </summary>
/// <remarks>
/// This is the Volcano GetRow, expressed over the step stream. A caller that only wants rows can ignore what is yielded and read
/// CurrentRecord, while the trace sees every page read and comparison the row cost. An operator reading from another operator therefore
/// needs to know nothing about how far below it the work actually happened.
/// </remarks>
public sealed class InputCursor(JoinInput input, int side, JoinStepIterator owner, bool collectsRows = true)
{
    public IRecord? CurrentRecord { get; private set; }

    public StopReason? StopReason { get; private set; }

    /// <summary>
    /// Advances to the next row, yielding the steps taken to reach it
    /// </summary>
    /// <remarks>
    /// Ends on the row, on the input stopping, or on the input running out of steps. CurrentRecord is null unless a row was reached.
    /// </remarks>
    public async IAsyncEnumerable<AccessStep> GetRowAsync()
    {
        CurrentRecord = null;

        while (true)
        {
            var step = await input.Iterator.StepNextAsync(owner.CurrentToken);

            if (step is null)
            {
                yield break;
            }

            if (step is AccessStep.Stopped stopped)
            {
                StopReason = stopped.Reason;

                yield break;
            }

            if (input.Iterator.GetOutputRow(step) is { } record)
            {
                CurrentRecord = record;

                if (collectsRows)
                {
                    input.Collect(record);
                }

                yield return owner.Observe(step, side);

                yield break;
            }

            yield return owner.Observe(step, side);
        }
    }
}
