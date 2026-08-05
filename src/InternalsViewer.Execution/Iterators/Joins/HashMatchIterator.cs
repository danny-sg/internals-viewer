using System.Numerics;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Interfaces.Iterators.Joins;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Iterators.Joins;

/// <summary>
/// Hash Match Steps
/// </summary>
/// <remarks>
/// A hash match reads its two inputs in separate phases, Build and Probe.
///
/// Steps are:
///
///     Build phase:
///
///     1. Read a Build row
///     2. Hash its join key
///     3. Add it to the bucket the hash selects
///     4. Repeat until the Build input is exhausted
///
///     Probe phase:
///
///     5. Read a Probe row
///     6. Hash its join key, selecting a bucket
///     7. Compare against each entry in that bucket
///         7a. Hashes differ -> no match
///         7b. Hashes match but keys differ -> no match, a wasted key comparison
///         7c. Keys match -> Match -> Emit row (Build + Probe)
///     8. Repeat until the Probe input is exhausted
///
///     Build scoped joins (Left/Full/Left Anti-Semi/Left Semi) walk the table once the probe is done, emitting the entries the probe
///     never reached, or for a semi join the entries it did.
///
/// A NULL key never equals anything. A Build row carrying one still takes a bucket, because an outer join has to find it again once the
/// probe is done, but the comparison rejects it. A Probe row carrying one never walks a bucket at all.
///
/// Partitioning and spilling are not simulated, so the table is built entirely in memory at one recursion level.
/// </remarks>
public sealed class HashMatchIterator(IIteratorFactory factory) : JoinIterator, IHashTableIterator
{
    public override PageAddress? CurrentPageAddress
        => IsProbePhase ? Inner.Iterator.CurrentPageAddress : Outer.Iterator.CurrentPageAddress;

    public HashTable Table { get; private set; } = new(JoinHash.DefaultBucketBits);

    public override IReadOnlyList<RowBuffer> Buffers => Inner is null ? [] : [new RowBuffer("Probe", 1, Inner.Buffer)];

    /// <summary>
    /// Rows the build side was expected to produce, which is what the table was sized for
    /// </summary>
    public long BuildRowEstimate { get; private set; }

    /// <summary>
    /// The join's own predicate, applied to a pair once its keys have matched
    /// </summary>
    private AccessPredicate? Residual { get; set; }

    private int? PendingBucketBits { get; set; }

    private bool IsProbePhase { get; set; }

    private IReadOnlyList<string> BuildColumns { get; set; } = [];

    private IReadOnlyList<string> ProbeColumns { get; set; } = [];

    private int CompareWidth { get; set; }

    /// <summary>
    /// Rebuilds the table at a different bucket count without restarting the walk
    /// </summary>
    /// <remarks>
    /// A resize that landed part way through a chain walk would leave the walk holding a bucket the table no longer owns, so the entry it
    /// went on to mark as matched would be the wrong one. Mid-walk the new count is held back and applied before the next row instead.
    /// </remarks>
    public void SetBucketCount(int bucketCount)
    {
        if (bucketCount < 2 || (bucketCount & (bucketCount - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "Bucket count must be a power of two.");
        }

        var bucketBits = BitOperations.Log2((uint)bucketCount);

        if (!IsOpen || IsComplete)
        {
            Table.Resize(bucketBits);

            return;
        }

        PendingBucketBits = bucketBits;
    }

    public override async Task OpenAsync(IteratorDefinition definition, 
                                         IteratorContext context,
                                         CancellationToken cancellationToken)
    {
        var join = definition.Expect<HashMatchDefinition>();

        if (IsOpen)
        {
            await CloseAsync();
        }

        await PrepareAsync(definition, context, cancellationToken);

        ResetJoin(join.JoinType);

        Outer = new JoinInput(factory.Create(join.Build.Source));
        Inner = new JoinInput(factory.Create(join.Probe.Source));

        BuildColumns = join.Build.JoinColumns;
        ProbeColumns = join.Probe.JoinColumns;

        CompareWidth = Math.Min(BuildColumns.Count, ProbeColumns.Count);

        BuildRowEstimate = join.Build.RowEstimate;

        Residual = join.Residual is AccessPredicate.NoTranslation ? null : join.Residual;

        Table = new HashTable(join.BucketBits ?? JoinHash.BucketBitsFor(BuildRowEstimate));

        PendingBucketBits = null;
        IsProbePhase = false;

        await Outer.Iterator.OpenAsync(join.Build.Source, context, cancellationToken);

        await Inner.Iterator.OpenAsync(join.Probe.Source, context, cancellationToken);

        StartRows();
    }

    protected override async IAsyncEnumerable<IRecord> RunAsync()
    {
        await EmitAsync(new AccessStep.JoinStart("Building hash table"), CurrentToken);

        var buildRow = await Outer.Iterator.GetRowAsync(CurrentToken);

        while (buildRow is not null)
        {
            ApplyPendingResize();

            var key = GetKey(buildRow, BuildColumns, "hash key");

            var hasNullKey = JoinHash.HasNull(key, CompareWidth);

            var hash = JoinHash.Compute(key, CompareWidth);

            var (bucket, entry) = Table.Add(hash, key, buildRow, hasNullKey);

            await EmitAsync(new AccessStep.HashBuild(bucket, hash)
                            {
                                Key = key,
                                Entry = entry,
                                ChainLength = Table.Buckets[bucket].Count,
                                IsNullKey = hasNullKey,
                                BucketCount = Table.BucketCount
                            }, 
                            CurrentToken);

            buildRow = await Outer.Iterator.GetRowAsync(CurrentToken);
        }

        IsProbePhase = true;

        await EmitAsync(new AccessStep.JoinStart("Probing hash table"), CurrentToken);

        var probeRow = await Inner.Iterator.GetRowAsync(CurrentToken);

        while (probeRow is not null)
        {
            Inner.Collect(probeRow);

            await foreach (var row in ProbeRowAsync(probeRow))
            {
                yield return row;
            }

            probeRow = await Inner.Iterator.GetRowAsync(CurrentToken);
        }

        if (JoinType.PreservesOuter() || JoinType.EmitsOuterOnMatch())
        {
            await EmitAsync(new AccessStep.JoinStart("Draining hash table"), CurrentToken);

            foreach (var bucket in Table.Buckets)
            {
                foreach (var entry in bucket.Entries)
                {
                    if (!entry.IsMatched && JoinType.PreservesOuter())
                    {
                        await EmitAsync(EmitUnmatched(entry.Record, null), CurrentToken);

                        yield return MakeRow(entry.Record, null);
                    }
                    else if (entry.IsMatched && JoinType.EmitsOuterOnMatch())
                    {
                        PairCount++;

                        await EmitAsync(new AccessStep.JoinEmit(PairCount) { OuterRecord = entry.Record }, CurrentToken);

                        yield return MakeRow(entry.Record, null);
                    }
                }
            }
        }

        var reason = Inner.Iterator.StopReason ?? Outer.Iterator.StopReason ?? AccessPaths.Results.StopReason.PageExhausted;

        await EmitAsync(new AccessStep.Stopped(reason), CurrentToken);
    }

    private async IAsyncEnumerable<IRecord> ProbeRowAsync(IRecord record)
    {
        ApplyPendingResize();

        var key = GetKey(record, ProbeColumns, "hash key");

        if (!JoinHash.TryCompute(key, CompareWidth, out var hash))
        {
            Inner.MarkState(record, JoinRowState.Finished);

            await EmitAsync(new AccessStep.HashProbe(-1, 0) { Key = key, IsNullKey = true }, CurrentToken);

            if (JoinType.PreservesInner())
            {
                await EmitAsync(EmitUnmatched(null, record), CurrentToken);

                yield return MakeRow(null, record);
            }

            yield break;
        }

        var bucket = Table.GetBucket(hash);

        await EmitAsync(new AccessStep.HashProbe(bucket.Index, hash) { Key = key, ChainLength = bucket.Count }, CurrentToken);

        var isMatched = false;

        for (var index = 0; index < bucket.Count; index++)
        {
            var entry = bucket.Entries[index];

            var isHashMatch = entry.Hash == hash;

            var isKeyMatch = isHashMatch && !entry.HasNullKey && entry.Key.ComparePrefix(key, CompareWidth) == 0;

            var isMatch = isKeyMatch && PassesResidual(entry.Record, record);

            await EmitAsync(new AccessStep.HashCompare(bucket.Index, index, isMatch)
                            {
                                ProbeKey = key,
                                BuildKey = entry.Key,
                                IsHashMatch = isHashMatch,
                                IsKeyMatch = isKeyMatch,
                                HasResidual = Residual is not null
                            }, 
                            CurrentToken);

            if (!isMatch)
            {
                continue;
            }

            isMatched = true;

            Table.MarkMatched(bucket.Index, index);

            Inner.MarkMatched(record);

            if (JoinType.EmitsPairs())
            {
                PairCount++;

                await EmitAsync(new AccessStep.JoinEmit(PairCount)
                                {
                                    OuterRecord = entry.Record,
                                    InnerRecord = record
                                }, 
                                CurrentToken);

                yield return MakeRow(entry.Record, record);
            }
            else if (!JoinType.PreservesOuter() && !JoinType.EmitsOuterOnMatch())
            {
                break;
            }
        }

        if (isMatched && JoinType.EmitsInnerOnMatch())
        {
            PairCount++;

            await EmitAsync(new AccessStep.JoinEmit(PairCount) { InnerRecord = record }, CurrentToken);

            yield return MakeRow(null, record);
        }

        if (!isMatched && JoinType.PreservesInner())
        {
            await EmitAsync(EmitUnmatched(null, record), CurrentToken);

            yield return MakeRow(null, record);
        }

        Inner.MarkState(record, JoinRowState.Finished);
    }

    /// <summary>
    /// Tests a pair that already matched on the hash key against the join's residual
    /// </summary>
    /// <remarks>
    /// A predicate that cannot be decided is treated as failing, matching how SQL three valued logic keeps an UNKNOWN row out of a join.
    /// </remarks>
    private bool PassesResidual(IRecord buildRecord, IRecord probeRecord)
    {
        if (Residual is null)
        {
            return true;
        }

        var row = new JoinRowValueSource(buildRecord, probeRecord);

        return PredicateEvaluator.Evaluate(Residual, row, Context.EvaluationContext) == true;
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

    private void ApplyPendingResize()
    {
        if (PendingBucketBits is { } bucketBits)
        {
            Table.Resize(bucketBits);

            PendingBucketBits = null;
        }
    }
}
