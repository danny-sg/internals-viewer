using System.Numerics;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Interfaces;
using InternalsViewer.Execution.Iterators.Joins.Inputs;
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
public sealed class HashMatchStepIterator(IIteratorFactory factory) : JoinStepIterator
{
    public const int BuildSource = OuterSource;

    public const int ProbeSource = InnerSource;

    public override PageAddress? CurrentPageAddress
        => Current?.Source == ProbeSource ? Inner.Iterator.CurrentPageAddress : Outer.Iterator.CurrentPageAddress;

    public HashTable Table { get; private set; } = new(JoinHash.DefaultBucketBits);

    /// <summary>
    /// Rows the build side was expected to produce, which is what the table was sized for
    /// </summary>
    public long BuildRowEstimate { get; private set; }

    /// <summary>
    /// The join's own predicate, applied to a pair once its keys have matched
    /// </summary>
    private AccessPredicate? Residual { get; set; }

    private EvaluationContext? CurrentEvaluationContext { get; set; }

    private int? PendingBucketBits { get; set; }

    private IReadOnlyList<string> BuildColumns { get; set; } = [];

    private IReadOnlyList<string> ProbeColumns { get; set; } = [];

    private int CompareWidth { get; set; }

    private AccessCounters BuildCounters { get; set; }

    private AccessCounters ProbeCounters { get; set; }

    private IAsyncEnumerator<AccessStep>? Steps { get; set; }

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

        if (Steps is null || IsComplete)
        {
            Table.Resize(bucketBits);

            return;
        }

        PendingBucketBits = bucketBits;
    }

    public override async Task OpenAsync(IteratorContext context, IteratorDefinition definition, CancellationToken cancellationToken)
    {
        var join = definition.Expect<HashMatchDefinition>();

        if (Steps is not null)
        {
            await CloseAsync();
        }

        var build = new IteratorJoinInput(factory.Create(join.Build.Source), join.Build.Source);

        var probe = new IteratorJoinInput(factory.Create(join.Probe.Source), join.Probe.Source);

        Outer = build;
        Inner = probe;

        ResetJoin(join.JoinType);

        BuildColumns = join.Build.JoinColumns;
        ProbeColumns = join.Probe.JoinColumns;

        CompareWidth = Math.Min(BuildColumns.Count, ProbeColumns.Count);

        BuildCounters = default;
        ProbeCounters = default;

        BuildRowEstimate = join.Build.RowEstimate;

        Residual = join.Residual;

        CurrentEvaluationContext = context.EvaluationContext;

        Table = new HashTable(join.BucketBits ?? JoinHash.BucketBitsFor(BuildRowEstimate));

        PendingBucketBits = null;

        await build.OpenAsync(context, cancellationToken);

        await probe.OpenAsync(context, cancellationToken);

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
        var build = new InputCursor(Outer, BuildSource, this, false);

        var probe = new InputCursor(Inner, ProbeSource, this);

        await foreach (var step in BuildAsync(build).WithCancellation(CurrentToken))
        {
            yield return step;
        }

        await foreach (var step in ProbeAsync(probe).WithCancellation(CurrentToken))
        {
            yield return step;
        }

        await foreach (var step in DrainTableAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }

        var reason = probe.StopReason ?? build.StopReason ?? StopReason.PageExhausted;

        yield return Stamp(new AccessStep.Stopped(reason), JoinSource);
    }

    private async IAsyncEnumerable<AccessStep> BuildAsync(InputCursor build)
    {
        yield return Stamp(new AccessStep.JoinStart("Building hash table"), JoinSource);

        await foreach (var step in build.GetRowAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }

        while (build.CurrentRecord is { } record)
        {
            ApplyPendingResize();

            var key = GetKey(record, BuildColumns);

            var hasNullKey = JoinHash.HasNull(key, CompareWidth);

            var hash = JoinHash.Compute(key, CompareWidth);

            var (bucket, entry) = Table.Add(hash, key, record, hasNullKey);

            yield return Stamp(new AccessStep.HashBuild(bucket, hash)
                               {
                                   Key = key,
                                   Entry = entry,
                                   ChainLength = Table.Buckets[bucket].Count,
                                   IsNullKey = hasNullKey
                               },
                               JoinSource);

            await foreach (var step in build.GetRowAsync().WithCancellation(CurrentToken))
            {
                yield return step;
            }
        }
    }

    private async IAsyncEnumerable<AccessStep> ProbeAsync(InputCursor probe)
    {
        yield return Stamp(new AccessStep.JoinStart("Probing hash table"), JoinSource);

        await foreach (var step in probe.GetRowAsync().WithCancellation(CurrentToken))
        {
            yield return step;
        }

        while (probe.CurrentRecord is { } record)
        {
            foreach (var step in ProbeRow(record))
            {
                yield return step;
            }

            await foreach (var step in probe.GetRowAsync().WithCancellation(CurrentToken))
            {
                yield return step;
            }
        }
    }

    private IEnumerable<AccessStep> ProbeRow(IRecord record)
    {
        ApplyPendingResize();

        var key = GetKey(record, ProbeColumns);

        if (!JoinHash.TryCompute(key, CompareWidth, out var hash))
        {
            Inner.MarkState(record, JoinRowState.Finished);

            yield return Stamp(new AccessStep.HashProbe(-1, 0) { Key = key, IsNullKey = true }, JoinSource);

            if (JoinType.PreservesInner())
            {
                yield return Stamp(EmitUnmatched(null, record), JoinSource);
            }

            yield break;
        }

        var bucket = Table.GetBucket(hash);

        yield return Stamp(new AccessStep.HashProbe(bucket.Index, hash) { Key = key, ChainLength = bucket.Count }, JoinSource);

        var isMatched = false;

        for (var index = 0; index < bucket.Count; index++)
        {
            var entry = bucket.Entries[index];

            var isHashMatch = entry.Hash == hash;

            var isKeyMatch = isHashMatch && !entry.HasNullKey && entry.Key.ComparePrefix(key, CompareWidth) == 0;

            var isMatch = isKeyMatch && PassesResidual(entry.Record, record);

            yield return Stamp(new AccessStep.HashCompare(bucket.Index, index, isMatch)
                               {
                                   ProbeKey = key,
                                   BuildKey = entry.Key,
                                   IsHashMatch = isHashMatch,
                                   IsKeyMatch = isKeyMatch,
                                   HasResidual = Residual is not null
                               },
                               JoinSource);

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

                yield return Stamp(new AccessStep.JoinEmit(PairCount)
                                   {
                                       OuterRecord = entry.Record,
                                       InnerRecord = record
                                   },
                                   JoinSource);
            }
            else if (!JoinType.PreservesOuter() && !JoinType.EmitsOuterOnMatch())
            {
                break;
            }
        }

        if (isMatched && JoinType.EmitsInnerOnMatch())
        {
            PairCount++;

            yield return Stamp(new AccessStep.JoinEmit(PairCount) { InnerRecord = record }, JoinSource);
        }

        if (!isMatched && JoinType.PreservesInner())
        {
            yield return Stamp(EmitUnmatched(null, record), JoinSource);
        }

        Inner.MarkState(record, JoinRowState.Finished);
    }

    private async IAsyncEnumerable<AccessStep> DrainTableAsync()
    {
        if (!JoinType.PreservesOuter() && !JoinType.EmitsOuterOnMatch())
        {
            yield break;
        }

        yield return Stamp(new AccessStep.JoinStart("Draining hash table"), JoinSource);

        foreach (var bucket in Table.Buckets)
        {
            foreach (var entry in bucket.Entries)
            {
                if (!entry.IsMatched && JoinType.PreservesOuter())
                {
                    yield return Stamp(EmitUnmatched(entry.Record, null), JoinSource);
                }
                else if (entry.IsMatched && JoinType.EmitsOuterOnMatch())
                {
                    PairCount++;

                    yield return Stamp(new AccessStep.JoinEmit(PairCount) { OuterRecord = entry.Record }, JoinSource);
                }
            }
        }

        await Task.CompletedTask;
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

        return PredicateEvaluator.Evaluate(Residual, row, CurrentEvaluationContext) == true;
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

    private static AccessKey GetKey(IRecord record, IReadOnlyList<string> columns)
    {
        var source = new RecordRowValueSource(record);

        var values = new AccessValue[columns.Count];

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];

            if (!record.Fields.Any(f => string.Equals(f.ColumnStructure.ColumnName, column, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Row has no column '{column}' to build the hash key");
            }

            values[index] = source.GetValue(-1, column).WithColumnName(column);
        }

        return new AccessKey([.. values]);
    }

    private AccessStep Stamp(AccessStep step, int source)
    {
        if (source == BuildSource)
        {
            BuildCounters = step.Counters;
        }
        else if (source == ProbeSource)
        {
            ProbeCounters = step.Counters;
        }

        return Attribute(step, source, BuildCounters.Add(ProbeCounters));
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
