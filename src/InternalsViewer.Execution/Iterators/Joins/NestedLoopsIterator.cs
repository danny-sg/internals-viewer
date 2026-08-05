using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Joins;

/// <summary>
/// Nested Loops Stepping
/// </summary>
/// <remarks>
/// Nested loops join two tables by iterating over the rows of the outer table and for each row iterating over the rows of the inner table,
/// checking for matches.
///
/// A rebind is performed at the start of each inner loop iteration, re-opening the inner iterator with the outer row on the context.
///
/// This service requires an outer input that can be scanned in key order, and an inner input that can be rebound for each outer row.
///
/// Note: from the perspective of the join there is no difference between a loop join and a key lookup.
/// </remarks>
public sealed class NestedLoopsIterator(IIteratorFactory factory) : JoinIterator
{
    public override PageAddress? CurrentPageAddress
        => IsInnerActive ? Inner.Iterator.CurrentPageAddress : Outer.Iterator.CurrentPageAddress;

    public int RebindCount { get; private set; }

    private IteratorDefinition InnerDefinition { get; set; } = null!;

    private bool FetchesDirectly { get; set; }

    private bool IsInnerActive { get; set; }

    public override async Task OpenAsync(IteratorDefinition definition, 
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var join = definition.Expect<NestedLoopsDefinition>();

        if (join.Inner is not (SeekDefinition or HeapFetchDefinition))
        {
            throw new ArgumentException($"A nested loops join drives its inner side by rebinding, which a {join.Inner.GetType().Name} "
                                        + "does not support");
        }

        if (IsOpen)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        ResetJoin(join.JoinType);

        Outer = new JoinInput(factory.Create(join.Outer));
        Inner = new JoinInput(factory.Create(join.Inner));

        InnerDefinition = join.Inner;

        FetchesDirectly = join.Inner is HeapFetchDefinition;

        RebindCount = 0;
        IsInnerActive = false;

        await Outer.Iterator.OpenAsync(join.Outer, context, cancellationToken);

        StartRows();
    }

    protected override async IAsyncEnumerable<IRecord> RunAsync()
    {
        await EmitAsync(new AccessStep.JoinStart($"Starting Nested Loops Join ({JoinType.ToDisplayName()})"), CurrentToken);

        while (await Outer.Iterator.GetRowAsync(CurrentToken) is { } outerRow)
        {
            Outer.Clear();
            Outer.Hold(outerRow, JoinRowState.Pending);

            Inner.Clear();

            RebindCount++;

            await Inner.Iterator.OpenAsync(InnerDefinition, Context with { CorrelatedRow = outerRow }, CurrentToken);

            IsInnerActive = true;

            var pairsBefore = PairCount;

            var hasInner = false;

            while (await Inner.Iterator.GetRowAsync(CurrentToken) is { } innerRow)
            {
                hasInner = true;

                Inner.Hold(innerRow, JoinRowState.Matched);

                if (JoinType.EmitsPairs())
                {
                    PairCount++;

                    if (!FetchesDirectly)
                    {
                        await EmitAsync(new AccessStep.JoinVerdict(JoinType.Decide(true, true)), CurrentToken);
                    }

                    await EmitAsync(new AccessStep.JoinEmit(PairCount)
                                    {
                                        OuterRecord = outerRow,
                                        InnerRecord = innerRow
                                    },
                                    CurrentToken);

                    yield return MakeRow(outerRow, innerRow);
                }
                else if (JoinType.EmitsOuterOnMatch())
                {
                    break;
                }
            }

            IsInnerActive = false;

            Outer.MarkState(outerRow, hasInner ? JoinRowState.Matched : JoinRowState.Finished);

            var emitsBeyondPairs = (hasInner && JoinType.EmitsOuterOnMatch()) || (!hasInner && JoinType.PreservesOuter());

            if (PairCount == pairsBefore && (!FetchesDirectly || !emitsBeyondPairs))
            {
                await EmitAsync(new AccessStep.JoinVerdict(JoinType.Decide(true, hasInner)), CurrentToken);
            }

            if (hasInner && JoinType.EmitsOuterOnMatch())
            {
                PairCount++;

                await EmitAsync(new AccessStep.JoinEmit(PairCount) { OuterRecord = outerRow }, CurrentToken);

                yield return MakeRow(outerRow, null);
            }
            else if (!hasInner && JoinType.PreservesOuter())
            {
                PairCount++;

                await EmitAsync(new AccessStep.JoinEmit(PairCount)
                                {
                                    OuterRecord = outerRow,
                                    IsUnmatched = true
                                }, 
                                CurrentToken);

                yield return MakeRow(outerRow, null);
            }
        }

        await EmitAsync(new AccessStep.Stopped(Outer.Iterator.StopReason ?? AccessPaths.Results.StopReason.PageExhausted), CurrentToken);
    }
}
