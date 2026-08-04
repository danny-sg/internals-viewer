using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.Interfaces;
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
public sealed class MergeJoinIterator(IIteratorFactory factory) : JoinIterator
{
    public override PageAddress? CurrentPageAddress
        => IsInnerCurrent ? Inner.Iterator.CurrentPageAddress : Outer.Iterator.CurrentPageAddress;

    private bool IsInnerCurrent { get; set; }

    private List<IRecord> InnerBuffer { get; } = [];

    private IReadOnlyList<string> OuterColumns { get; set; } = [];

    private IReadOnlyList<string> InnerColumns { get; set; } = [];

    private int CompareWidth { get; set; }

    private int ComparisonSign { get; set; } = 1;

    public override async Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
    {
        var join = definition.Expect<MergeJoinDefinition>();

        if (IsOpen)
        {
            await CloseAsync();
        }

        await PrepareAsync(context, definition, cancellationToken);

        ResetJoin(join.JoinType);

        Outer = new JoinInput(factory.Create(join.Outer.Source));
        Inner = new JoinInput(factory.Create(join.Inner.Source));

        OuterColumns = join.Outer.JoinColumns;
        InnerColumns = join.Inner.JoinColumns;

        CompareWidth = Math.Min(OuterColumns.Count, InnerColumns.Count);

        ComparisonSign = join.Outer.Direction == ScanDirection.Backward ? -1 : 1;

        IsInnerCurrent = false;

        InnerBuffer.Clear();

        await Outer.Iterator.OpenAsync(context, join.Outer.Source, cancellationToken);

        await Inner.Iterator.OpenAsync(context, join.Inner.Source, cancellationToken);

        StartRows();
    }

    protected override async IAsyncEnumerable<IRecord> RunAsync()
    {
        await EmitAsync(new AccessStep.JoinStart("Reading Outer"), CurrentToken);

        var outerRow = await AdvanceAsync(Outer);

        await EmitAsync(new AccessStep.JoinStart("Reading Inner"), CurrentToken);

        var innerRow = await AdvanceAsync(Inner);

        while (outerRow is not null && innerRow is not null)
        {
            var outerKey = GetKey(outerRow, OuterColumns, "merge key");

            var innerKey = GetKey(innerRow, InnerColumns, "merge key");

            // A null key never equals anything, so the row is unmatched whatever the other side holds
            var comparison = HasNull(outerKey) ? -1
                             : HasNull(innerKey) ? 1
                             : outerKey.ComparePrefix(innerKey, CompareWidth) * ComparisonSign;

            if (comparison < 0)
            {
                // Inner is ahead of Outer, the outer record is marked as finished
                Outer.MarkState(outerRow, JoinRowState.Finished);

                await EmitAsync(Compare(outerKey, innerKey, comparison, "Outer < Inner"), CurrentToken);

                if (JoinType.PreservesOuter())
                {
                    // Join preserves Outer -> emit unmatched row
                    await EmitAsync(EmitUnmatched(outerRow, null), CurrentToken);

                    yield return MakeRow(outerRow, null);
                }

                outerRow = await AdvanceAsync(Outer);
            }
            else if (comparison > 0)
            {
                // Outer is ahead of Inner, the inner record is marked as finished
                Inner.MarkState(innerRow, JoinRowState.Finished);

                await EmitAsync(Compare(outerKey, innerKey, comparison, "Inner < Outer"), CurrentToken);

                if (JoinType.PreservesInner())
                {
                    // Join preserves Inner -> emit unmatched row
                    await EmitAsync(EmitUnmatched(null, innerRow), CurrentToken);

                    yield return MakeRow(null, innerRow);
                }

                innerRow = await AdvanceAsync(Inner);
            }
            else
            {
                Outer.MarkMatched(outerRow);
                Inner.MarkMatched(innerRow);

                await EmitAsync(Compare(outerKey, innerKey, 0, "Outer = Inner"), CurrentToken);

                var bufferKey = innerKey;

                InnerBuffer.Clear();

                // Relationship between Outer and Inner could have multiple records matching, so Inner is read ahead into a buffer where
                // read ahead continues until the Inner no longer matches
                while (innerRow is not null && GetKey(innerRow, InnerColumns, "merge key").ComparePrefix(bufferKey, CompareWidth) == 0)
                {
                    InnerBuffer.Add(innerRow);

                    Inner.MarkMatched(innerRow);

                    innerRow = await AdvanceAsync(Inner);
                }

                var isFirstOuter = true;

                // Outer is read forwards while the key matches
                while (outerRow is not null && GetKey(outerRow, OuterColumns, "merge key").ComparePrefix(bufferKey, CompareWidth) == 0)
                {
                    Outer.MarkMatched(outerRow);

                    // Inner / Left / Right / Full joins
                    if (JoinType.EmitsPairs()) 
                    {
                        // Emit a pair for each buffered Inner record
                        foreach (var groupRecord in InnerBuffer)
                        {
                            PairCount++;

                            await EmitAsync(new AccessStep.JoinEmit(PairCount)
                                            {
                                                OuterRecord = outerRow,
                                                InnerRecord = groupRecord,
                                                IsFromBuffer = !isFirstOuter
                                            }, 
                                            CurrentToken);

                            yield return MakeRow(outerRow, groupRecord);
                        }
                    }
                    else if (JoinType.EmitsOuterOnMatch()) 
                    {
                        // Left Semi-Join
                        PairCount++;

                        // Emit only the Outer record
                        await EmitAsync(new AccessStep.JoinEmit(PairCount) { OuterRecord = outerRow }, CurrentToken);

                        yield return MakeRow(outerRow, null);
                    }
                    else if (JoinType.EmitsInnerOnMatch() && isFirstOuter)
                    {
                        // Right Semi-Join
                        foreach (var groupRecord in InnerBuffer)
                        {
                            PairCount++;

                            // Emit only the buffered inner records
                            await EmitAsync(new AccessStep.JoinEmit(PairCount) { InnerRecord = groupRecord }, CurrentToken);

                            yield return MakeRow(null, groupRecord);
                        }
                    }

                    isFirstOuter = false;

                    outerRow = await AdvanceAsync(Outer);
                }

                InnerBuffer.Clear();

                ResetBuffers(outerRow, innerRow);
            }
        }

        // Outer scoped joins (Left/Full/Left Anti-Semi) emit Outer for each record when unmatched
        if (JoinType.PreservesOuter())
        {
            while (outerRow is not null)
            {
                Outer.MarkState(outerRow, JoinRowState.Finished);

                await EmitAsync(EmitUnmatched(outerRow, null), CurrentToken);

                yield return MakeRow(outerRow, null);

                outerRow = await AdvanceAsync(Outer);
            }
        }

        // Inner scoped joins (Right/Full/Right Anti-Semi) emit Inner for each record when unmatched
        if (JoinType.PreservesInner())
        {
            while (innerRow is not null)
            {
                Inner.MarkState(innerRow, JoinRowState.Finished);

                await EmitAsync(EmitUnmatched(null, innerRow), CurrentToken);

                yield return MakeRow(null, innerRow);

                innerRow = await AdvanceAsync(Inner);
            }
        }

        var reason = Outer.Iterator.CurrentRow is null
                     ? Outer.Iterator.StopReason ?? AccessPaths.Results.StopReason.PageExhausted
                     : Inner.Iterator.StopReason ?? AccessPaths.Results.StopReason.PageExhausted;

        await EmitAsync(new AccessStep.Stopped(reason), CurrentToken);
    }

    private async Task<IRecord?> AdvanceAsync(JoinInput input)
    {
        IsInnerCurrent = ReferenceEquals(input, Inner);

        var row = await input.Iterator.GetRowAsync(CurrentToken);

        if (row is not null)
        {
            input.Collect(row);
        }

        return row;
    }

    /// <summary>
    /// Drops the rows a completed pairing consumed, keeping the row each side has already read past it
    /// </summary>
    private void ResetBuffers(IRecord? outerRow, IRecord? innerRow)
    {
        Outer.Clear();
        Inner.Clear();

        if (outerRow is not null)
        {
            Outer.Hold(outerRow, JoinRowState.Pending);
        }

        if (innerRow is not null)
        {
            Inner.Hold(innerRow, JoinRowState.Pending);
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
}
