using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators.Joins.Inputs;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Joins;

/// <summary>
/// Merge Join Steps
/// </summary>
/// <remarks>
/// Merge joins rely on two ordered inputs, Outer and Inner.
///
/// Steps are:
///
///     Inner join:
///
///     1. Move Outer forward
///     2. Move Inner forward
///     3. Keys are compared
///         3a. If Inner less than Outer
///             - move inner forward
///         3b. If Inner = Outer -> Match -> Emit row (Outer + Inner)
///             - Inner rows sharing the key are buffered as a group
///             - The group is emitted again for every Outer row with the same key
///             - Move outer forward
///         3c. If Inner greater than Outer -> move outer forward
///     4. Sequence continues until an input is exhausted
///
///     Left join:
///
///         3c. If Inner greater than Outer -> Emit row (Outer + NULL)
///             - Move outer forward
///         - Remaining Outer rows after Inner is exhausted are emitted as (Outer + NULL)
///
///     Right join:
///
///         3a. If Inner less than Outer -> Emit row (NULL + Inner)
///             - move inner forward
///         - Remaining Inner rows after Outer is exhausted are emitted as (NULL + Inner)
///
///     Left semi join:
///
///         3b. On a match, emit the Outer row only, once per matching Outer row
///             - The Inner group is consumed but never emitted
///
///     Right semi join:
///
///         3b. On a match, emit each Inner group row only, once (on the first matching Outer row)
///             - Subsequent Outer rows with the same key are consumed without emitting
///
///     Left anti-semi join:
///
///         3b. On a match, nothing is emitted and both sides are consumed
///         3c. If Inner greater than Outer -> Emit the Outer row (it has no partner)
///         - Remaining Outer rows after Inner is exhausted are also emitted
///
///     Right anti-semi join:
///
///         3b. On a match, nothing is emitted and both sides are consumed
///         3a. If Inner less than Outer -> Emit the Inner row (it has no partner)
///         - Remaining Inner rows after Outer is exhausted are also emitted
///
/// A NULL key never equals anything, so a row with a NULL key is treated as unmatched regardless of what the other side holds.
///
/// Outer and inner rows are held in a buffer for their usage lifetime in the join. When they are no longer needed as the cursor has moved
/// on they are drained from the buffer.
/// 
/// </remarks>
public sealed class MergeJoinStepIterator(IIteratorFactory factory) : JoinStepIterator
{
    public override PageAddress? CurrentPageAddress
        => Current?.Source == InnerSource ? Inner.Iterator.CurrentPageAddress : Outer.Iterator.CurrentPageAddress;

    private InputCursor? OuterCursor { get; set; }

    private InputCursor? InnerCursor { get; set; }

    private List<IRecord> InnerBuffer { get; } = [];

    private IReadOnlyList<string> OuterColumns { get; set; } = [];

    private IReadOnlyList<string> InnerColumns { get; set; } = [];

    private int CompareWidth { get; set; }

    private int ComparisonSign { get; set; } = 1;

    private AccessCounters OuterCounters { get; set; }

    private AccessCounters InnerCounters { get; set; }

    private IAsyncEnumerator<AccessStep>? Steps { get; set; }

    public override async Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
    {
        var join = definition.Expect<MergeJoinDefinition>();

        if (Steps is not null)
        {
            await CloseAsync();
        }

        var outer = new IteratorJoinInput(factory.Create(join.Outer.Source), join.Outer.Source);

        var inner = new IteratorJoinInput(factory.Create(join.Inner.Source), join.Inner.Source);

        Outer = outer;
        Inner = inner;

        ResetJoin(join.JoinType);

        OuterColumns = join.Outer.JoinColumns;
        InnerColumns = join.Inner.JoinColumns;

        CompareWidth = Math.Min(OuterColumns.Count, InnerColumns.Count);

        // Only an access path has a direction, so an input that is itself an operator is taken to preserve ascending key order
        ComparisonSign = join.Outer.Source is RangeDefinition { Direction: ScanDirection.Backward } ? -1 : 1;

        OuterCounters = default;
        InnerCounters = default;

        OuterCursor = null;
        InnerCursor = null;

        InnerBuffer.Clear();

        // Move outer forward
        await outer.OpenAsync(context, cancellationToken);

        // Move inner forward
        await inner.OpenAsync(context, cancellationToken);

        Steps = Run().GetAsyncEnumerator(CancellationToken.None);
    }

    public override async Task CloseAsync()
    {
        if (Steps is not null)
        {
            await Steps.DisposeAsync();

            Steps = null;
        }

        await base.CloseAsync();
    }

    public override async Task<AccessStep?> StepNextAsync(CancellationToken cancellationToken)
    {
        if (IsComplete || Steps is null)
        {
            return null;
        }

        CurrentToken = cancellationToken;

        if (!await Steps.MoveNextAsync())
        {
            IsComplete = true;

            return null;
        }

        var step = Steps.Current;

        StepHistory.Add(step);

        if (step is AccessStep.Stopped)
        {
            IsComplete = true;
        }

        return step;
    }

    internal override AccessStep Observe(AccessStep step, int side) => Stamp(step, side);

    private async IAsyncEnumerable<AccessStep> Run()
    {
        var outer = OuterCursor = new InputCursor(Outer, OuterSource, this);

        var inner = InnerCursor = new InputCursor(Inner, InnerSource, this);

        await foreach (var step in StartJoinAsync(outer, inner).WithCancellation(CurrentToken))
        {
            yield return step;
        }

        while (outer.CurrentRecord is { } outerRecord && inner.CurrentRecord is { } innerRecord)
        {
            var outerKey = GetKey(outerRecord, OuterColumns);

            var innerKey = GetKey(innerRecord, InnerColumns);

            // A null key never equals anything, so the row is unmatched whatever the other side holds
            var comparison = HasNull(outerKey) ? -1
                             : HasNull(innerKey) ? 1
                             : outerKey.ComparePrefix(innerKey, CompareWidth) * ComparisonSign;

            var steps = comparison switch
            {
                < 0 
                    => StepOuterBehindAsync(outer, outerRecord, outerKey, innerKey, comparison),
                > 0 
                    => StepInnerBehindAsync(inner, innerRecord, outerKey, innerKey, comparison),
                _ 
                    => StepMatchAsync(outer, inner, outerRecord, innerRecord, outerKey, innerKey)
            };

            await foreach (var step in steps.WithCancellation(CurrentToken))
            {
                yield return step;
            }
        }

        // Outer scoped joins (Left/Full/Left Anti-Semi) emit Outer for each record when unmatched
        if (JoinType.PreservesOuter())
        {
            await foreach (var step in DrainAsync(outer, isOuter: true).WithCancellation(CurrentToken))
            {
                yield return step;
            }
        }

        // Inner scoped joins (Right/Full/Right Anti-Semi) emit Inner for each record when unmatched
        if (JoinType.PreservesInner())
        {
            await foreach (var step in DrainAsync(inner, isOuter: false).WithCancellation(CurrentToken))
            {
                yield return step;
            }
        }

        var reason = outer.CurrentRecord is null
                     ? outer.StopReason ?? StopReason.PageExhausted
                     : inner.StopReason ?? StopReason.PageExhausted;

        yield return Stamp(new AccessStep.Stopped(reason), JoinSource);
    }

    private async IAsyncEnumerable<AccessStep> StartJoinAsync(InputCursor outer, InputCursor inner)
    {
        yield return Stamp(new AccessStep.JoinStart("Reading Outer"), JoinSource);

        await foreach (var step in outer.GetRowAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }

        yield return Stamp(new AccessStep.JoinStart("Reading Inner"), JoinSource);

        await foreach (var step in inner.GetRowAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }
    }

    private async IAsyncEnumerable<AccessStep> StepOuterBehindAsync(InputCursor outer,
                                                                    IRecord outerRecord,
                                                                    AccessKey outerKey,
                                                                    AccessKey innerKey,
                                                                    int comparison)
    {
        // Inner is ahead of Outer, the outer record is marked as finished
        Outer.MarkState(outerRecord, JoinRowState.Finished);

        yield return Stamp(Compare(outerKey, innerKey, comparison, "Outer < Inner"), JoinSource);

        if (JoinType.PreservesOuter())
        {
            // Join preserves Outer -> emit unmatched row
            yield return Stamp(EmitUnmatched(outerRecord, null), JoinSource);
        }

        // Advance Outer (still behind Inner)
        await foreach (var step in outer.GetRowAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }
    }

    private async IAsyncEnumerable<AccessStep> StepInnerBehindAsync(InputCursor inner,
                                                                    IRecord innerRecord,
                                                                    AccessKey outerKey,
                                                                    AccessKey innerKey,
                                                                    int comparison)
    {
        // Outer is ahead of Inner, the inner record is marked as finished
        Inner.MarkState(innerRecord, JoinRowState.Finished);

        yield return Stamp(Compare(outerKey, innerKey, comparison, "Inner < Outer"), JoinSource);

        if (JoinType.PreservesInner())
        {
            // Join preserves Inner -> emit unmatched row
            yield return Stamp(EmitUnmatched(null, innerRecord), JoinSource);
        }

        // Advance Inner (still behind Outer)
        await foreach (var step in inner.GetRowAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }
    }

    private async IAsyncEnumerable<AccessStep> StepMatchAsync(InputCursor outer,
                                                              InputCursor inner,
                                                              IRecord outerRecord,
                                                              IRecord innerRecord,
                                                              AccessKey outerKey,
                                                              AccessKey innerKey)
    {
        Outer.MarkMatched(outerRecord);
        Inner.MarkMatched(innerRecord);

        yield return Stamp(Compare(outerKey, innerKey, 0, "Outer = Inner"), JoinSource);

        await foreach (var step in BufferInnerGroupAsync(inner, innerKey).WithCancellation(CurrentToken))
        {
            yield return step;
        }

        await foreach (var step in EmitMatchedGroupAsync(outer, innerKey).WithCancellation(CurrentToken))
        {
            yield return step;
        }

        InnerBuffer.Clear();

        ResetBuffers();
    }

    private async IAsyncEnumerable<AccessStep> BufferInnerGroupAsync(InputCursor inner, AccessKey bufferKey)
    {
        InnerBuffer.Clear();

        // Relationship between Outer and Inner could have multiple records matching, so Inner is read ahead into a buffer where read ahead
        // continues until the Inner no longer matches
        while (inner.CurrentRecord is { } groupRecord
               && GetKey(groupRecord, InnerColumns).ComparePrefix(bufferKey, CompareWidth) == 0)
        {
            InnerBuffer.Add(groupRecord);

            Inner.MarkMatched(groupRecord);

            // Advance that ends the group has read a row for the next comparison, which is held back until it can be marked as such
            var steps = new List<AccessStep>();

            await foreach (var step in inner.GetRowAsync().WithCancellation(CurrentToken))
            {
                steps.Add(step);
            }

            var isReadAhead = inner.CurrentRecord is not { } next
                              || GetKey(next, InnerColumns).ComparePrefix(bufferKey, CompareWidth) != 0;

            foreach (var step in steps)
            {
                yield return isReadAhead && step is AccessStep.Row row
                    ? row with { IsReadAhead = true }
                    : step;
            }
        }
    }

    private async IAsyncEnumerable<AccessStep> EmitMatchedGroupAsync(InputCursor outer, AccessKey bufferKey)
    {
        var isFirstOuter = true;

        // Outer is read forwards while the key matches
        while (outer.CurrentRecord is { } matchRecord
               && GetKey(matchRecord, OuterColumns).ComparePrefix(bufferKey, CompareWidth) == 0)
        {
            Outer.MarkMatched(matchRecord);

            if (JoinType.EmitsPairs()) // Inner / Left / Right / Full joins
            {
                // Emit a pair for each buffered Inner record
                foreach (var groupRecord in InnerBuffer)
                {
                    PairCount++;

                    yield return Stamp(new AccessStep.JoinEmit(PairCount)
                                       {
                                           OuterRecord = matchRecord,
                                           InnerRecord = groupRecord,
                                           IsFromBuffer = !isFirstOuter
                                       },
                                       JoinSource);
                }
            }
            else if (JoinType.EmitsOuterOnMatch()) // Left Semi-Join
            {
                PairCount++;

                // Emit only the Outer record
                yield return Stamp(new AccessStep.JoinEmit(PairCount) { OuterRecord = matchRecord },
                                   JoinSource);
            }
            else if (JoinType.EmitsInnerOnMatch() && isFirstOuter) // Right Semi-Join
            {
                foreach (var groupRecord in InnerBuffer)
                {
                    PairCount++;

                    // Emit only the buffered inner records
                    yield return Stamp(new AccessStep.JoinEmit(PairCount) { InnerRecord = groupRecord },
                                       JoinSource);
                }
            }

            isFirstOuter = false;

            // Advance outer
            await foreach (var step in outer.GetRowAsync().WithCancellation(CurrentToken))
            {
                yield return step;
            }
        }
    }

    /// <summary>
    /// Drain remaining rows from a source cursor, emitting as unmatched
    /// </summary>
    private async IAsyncEnumerable<AccessStep> DrainAsync(InputCursor cursor, bool isOuter)
    {
        var input = isOuter ? Outer : Inner;

        while (cursor.CurrentRecord is { } remaining)
        {
            input.MarkState(remaining, JoinRowState.Finished);

            yield return Stamp(isOuter ? EmitUnmatched(remaining, null) : EmitUnmatched(null, remaining),
                               JoinSource);

            await foreach (var step in cursor.GetRowAsync().WithCancellation(CurrentToken))
            {
                yield return step;
            }
        }
    }

    private AccessStep.JoinEmit EmitUnmatched(IRecord? outerRecord, IRecord? innerRecord)
    {
        PairCount++;

        return new AccessStep.JoinEmit(PairCount)
        {
            OuterRecord = outerRecord,
            InnerRecord = innerRecord,
            IsUnmatched = true
        };
    }

    private static bool HasNull(AccessKey key)
    {
        for (var index = 0; index < key.Count; index++)
        {
            if (key[index].IsNull)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Drops the rows a completed pairing consumed, keeping the row each side has already read past it
    /// </summary>
    private void ResetBuffers()
    {
        Outer.Clear();
        Inner.Clear();

        if (OuterCursor?.CurrentRecord is { } outerRecord)
        {
            Outer.Hold(outerRecord, JoinRowState.Pending);
        }

        if (InnerCursor?.CurrentRecord is { } innerRecord)
        {
            Inner.Hold(innerRecord, JoinRowState.Pending);
        }
    }

    private AccessStep.MergeCompare Compare(AccessKey outerKey, AccessKey innerKey, int comparison, string action)
    {
        return new AccessStep.MergeCompare(comparison)
        {
            OuterKey = outerKey,
            InnerKey = innerKey,
            Action = action,

            // A comparison that advances one side has proven that side's row has no partner, so it is a verdict on that row
            Decision = JoinType.Decide(comparison <= 0, comparison >= 0)
        };
    }

    private static AccessKey GetKey(IRecord record, IReadOnlyList<string> columns)
    {
        var source = new RecordRowValueSource(record);

        var values = new AccessValue[columns.Count];

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];

            if (!record.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Row has no column '{column}' to build the merge key");
            }

            values[index] = source.GetValue(-1, column).WithColumnName(column);
        }

        return new AccessKey([.. values]);
    }

    private AccessStep Stamp(AccessStep step, int source)
    {
        if (source == OuterSource)
        {
            OuterCounters = step.Counters;
        }
        else if (source == InnerSource)
        {
            InnerCounters = step.Counters;
        }

        return Attribute(step, source, OuterCounters.Add(InnerCounters));
    }

}
