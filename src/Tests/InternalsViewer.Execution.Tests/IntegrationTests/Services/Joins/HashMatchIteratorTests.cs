using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using System.Data;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Execution.AccessPaths.Joins.Hash;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

public class HashMatchIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    [RequiresFileFact(MdfPath)]
    public async Task Overlapping_Ranges_Emit_Matching_Pairs()
    {
        var context = await LoadNumberTableAsync();

        var (steps, pairs) = await RunAsync(context, Definition(context));

        Assert.Equal(6, pairs.Count);
        Assert.Equal(6, context.Service.PairCount);
        Assert.Equal(Enumerable.Range(105, 6).Select(v => ((long)v, (long)v)), pairs.OrderBy(p => p.Build));

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(2, stopped.NodeId);
        Assert.True(context.Service.IsComplete);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Disjoint_Ranges_Emit_Nothing()
    {
        var context = await LoadNumberTableAsync();

        var definition = new HashMatchDefinition(SideInput(context.Unit, Between(100, 105), 0),
                                                 SideInput(context.Unit, Between(200, 205), 1)) { NodeId = 2 };

        var (steps, pairs) = await RunAsync(context, definition);

        Assert.Empty(pairs);

        Assert.NotEmpty(steps.OfType<AccessStep.HashProbe>());
        Assert.All(steps.OfType<AccessStep.HashCompare>(), c => Assert.False(c.IsMatch));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Identical_Ranges_Pair_Every_Row()
    {
        var context = await LoadNumberTableAsync();

        var definition = new HashMatchDefinition(SideInput(context.Unit, Between(500, 520), 0),
                                                 SideInput(context.Unit, Between(500, 520), 1)) { NodeId = 2 };

        var (steps, pairs) = await RunAsync(context, definition);

        Assert.Equal(21, pairs.Count);
        Assert.All(pairs, p => Assert.Equal(p.Build, p.Probe));

        Assert.Equal(21, steps.OfType<AccessStep.HashCompare>().Count(c => c.IsMatch));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Build_Completes_Before_The_First_Probe()
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, Definition(context));

        var lastBuild = steps.FindLastIndex(s => s is AccessStep.HashBuild);

        var firstProbe = steps.FindIndex(s => s is AccessStep.HashProbe);

        Assert.True(lastBuild >= 0 && firstProbe >= 0);
        Assert.True(lastBuild < firstProbe, "every build row should be hashed before the first probe");

        var buildRowsAfterProbe = steps.Skip(firstProbe)
                                       .Count(s => s is AccessStep.Row { EmittedRecord: not null, NodeId: 0 });

        Assert.Equal(0, buildRowsAfterProbe);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Every_Build_Row_Is_Added_To_The_Table()
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, Definition(context));

        var builds = steps.OfType<AccessStep.HashBuild>().ToList();

        Assert.Equal(11, builds.Count);
        Assert.All(builds, b => Assert.False(b.IsNullKey));

        Assert.Equal(11, context.Service.Table.RowCount);
        Assert.Equal(11, context.Service.Table.Buckets.Sum(b => b.Count));

        TestOutput.WriteLine($"buckets {context.Service.Table.BucketCount}, longest chain {context.Service.Table.LongestChain}");
    }

    [RequiresFileFact(MdfPath)]
    public async Task Probe_Only_Compares_Within_Its_Own_Bucket()
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, Definition(context));

        var bucket = -1;

        var comparisons = 0;

        foreach (var step in steps)
        {
            switch (step)
            {
                case AccessStep.HashProbe probe:
                    bucket = probe.Bucket;

                    break;

                case AccessStep.HashCompare compare:
                    Assert.Equal(bucket, compare.Bucket);

                    comparisons++;

                    break;
            }
        }

        Assert.True(comparisons > 0);

        var table = context.Service.Table;

        Assert.True(comparisons <= table.LongestChain * steps.OfType<AccessStep.HashProbe>().Count());
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Outer_Emits_Unmatched_Build_Rows()
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, Definition(context) with { JoinType = JoinType.LeftOuter });

        var unmatched = steps.OfType<AccessStep.JoinEmit>()
                             .Where(e => e.IsUnmatched)
                             .ToList();

        Assert.Equal(5, unmatched.Count);
        Assert.All(unmatched, e => Assert.NotNull(e.OuterRecord));
        Assert.All(unmatched, e => Assert.Null(e.InnerRecord));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Resizing_Redistributes_Every_Row()
    {
        var context = await LoadNumberTableAsync();

        var definition = new HashMatchDefinition(SideInput(context.Unit, Between(100, 160), 0),
                                                 SideInput(context.Unit, Between(105, 120), 1)) { NodeId = 2 };

        await RunAsync(context, definition);

        var table = context.Service.Table;

        Assert.Equal(16, table.BucketCount);

        var rowCount = table.RowCount;

        var longestBefore = table.LongestChain;

        Assert.Equal(61, rowCount);

        context.Service.SetBucketCount(64);

        Assert.Equal(64, table.BucketCount);
        Assert.Equal(rowCount, table.RowCount);
        Assert.Equal(rowCount, table.Buckets.Sum(b => b.Count));

        Assert.All(table.Buckets,
                   bucket => Assert.All(bucket.Entries,
                                        entry => Assert.Equal(bucket.Index, JoinHash.GetBucket(entry.Hash, 6))));

        Assert.True(table.LongestChain <= longestBefore);

        TestOutput.WriteLine($"longest chain: {longestBefore} at 16 buckets, {table.LongestChain} at 64");
    }

    [RequiresFileFact(MdfPath)]
    public async Task Resizing_Keeps_Match_Flags()
    {
        var context = await LoadNumberTableAsync();

        await RunAsync(context, Definition(context));

        var table = context.Service.Table;

        var matchedBefore = table.Buckets.Sum(b => b.Entries.Count(e => e.IsMatched));

        Assert.Equal(6, matchedBefore);

        context.Service.SetBucketCount(128);

        Assert.Equal(matchedBefore, table.Buckets.Sum(b => b.Entries.Count(e => e.IsMatched)));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Semi_Emits_Each_Matched_Build_Row_Once()
    {
        var emits = await RunSemiAsync(JoinType.LeftSemi);

        Assert.All(emits, e => Assert.NotNull(e.OuterRecord));
        Assert.All(emits, e => Assert.Null(e.InnerRecord));

        Assert.Equal(Enumerable.Range(105, 6).Select(v => (long)v), BuildIds(emits));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Anti_Semi_Emits_Build_Rows_With_No_Partner()
    {
        var emits = await RunSemiAsync(JoinType.LeftAntiSemi);

        Assert.All(emits, e => Assert.True(e.IsUnmatched));
        Assert.All(emits, e => Assert.Null(e.InnerRecord));

        Assert.Equal(Enumerable.Range(100, 5).Select(v => (long)v), BuildIds(emits));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Right_Semi_Emits_Each_Matched_Probe_Row_Once()
    {
        var emits = await RunSemiAsync(JoinType.RightSemi);

        Assert.All(emits, e => Assert.NotNull(e.InnerRecord));
        Assert.All(emits, e => Assert.Null(e.OuterRecord));

        Assert.Equal(Enumerable.Range(105, 6).Select(v => (long)v), ProbeIds(emits));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Right_Anti_Semi_Emits_Probe_Rows_With_No_Partner()
    {
        var emits = await RunSemiAsync(JoinType.RightAntiSemi);

        Assert.All(emits, e => Assert.True(e.IsUnmatched));
        Assert.All(emits, e => Assert.Null(e.OuterRecord));

        Assert.Equal(Enumerable.Range(111, 10).Select(v => (long)v), ProbeIds(emits));
    }

    /// <summary>
    /// A probe row matching more than one entry in a chain must mark every one of them, not just the first
    /// </summary>
    /// <remarks>
    /// CompressedTable's NumberField2 is the row number modulo ten, so a range of twenty rows holds each value twice and every build row
    /// has a partner. Stopping the walk at the first match would leave the second entry of each chain unmarked and halve the output.
    /// </remarks>
    [RequiresFileFact(MdfPath)]
    public async Task Left_Semi_Marks_Every_Matching_Entry_In_A_Chain()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var unit = DemoDatabase.Unit(database, DemoDatabase.CompressedTable, DemoDatabase.CompressedIndex);

        var service = serviceHost.GetService<HashMatchIterator>();

        var side = new JoinInputDefinition(new RangeDefinition(unit.AllocationUnitId, unit.RootPage, [BigIntBetween(1, 20)]),
                                          ["NumberField2"]);

        var definition = new HashMatchDefinition(side, side)
        {
            NodeId = 2,
            JoinType = JoinType.LeftSemi
        };

        var (steps, _) = await RunAsync(new IteratorStepper(service, definition, new IteratorContext(database)));

        Assert.Equal(20, service.Table.RowCount);

        Assert.Equal(20, steps.OfType<AccessStep.JoinEmit>().Count());
    }

    [RequiresFileFact(MdfPath)]
    public async Task A_Residual_Discards_Pairs_That_Already_Matched_On_The_Key()
    {
        var context = await LoadNumberTableAsync();

        var (steps, pairs) = await RunAsync(context, Definition(context) with { Residual = IdAtLeast(108) });

        Assert.Equal(3, pairs.Count);
        Assert.Equal(Enumerable.Range(108, 3).Select(v => ((long)v, (long)v)), pairs.OrderBy(p => p.Build));

        var keyMatches = steps.OfType<AccessStep.HashCompare>().Where(c => c.IsKeyMatch).ToList();

        Assert.Equal(6, keyMatches.Count);

        Assert.All(keyMatches, c => Assert.True(c.HasResidual));
        Assert.All(keyMatches, c => Assert.True(c.ShowsResidual));

        Assert.Equal(3, keyMatches.Count(c => c.IsResidualFail));
    }

    [RequiresFileFact(MdfPath)]
    public async Task A_Residual_Failure_Is_Not_Reported_As_A_Hash_Collision()
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, Definition(context) with { Residual = IdAtLeast(108) });

        var discarded = steps.OfType<AccessStep.HashCompare>().Where(c => c.IsResidualFail).ToList();

        Assert.NotEmpty(discarded);

        Assert.All(discarded, c => Assert.True(c.IsHashMatch));
        Assert.All(discarded, c => Assert.True(c.IsKeyMatch));
        Assert.All(discarded, c => Assert.False(c.IsMatch));
        Assert.All(discarded, c => Assert.False(c.IsFalsePositive));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Without_A_Residual_No_Residual_Verdict_Is_Reported()
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, Definition(context));

        Assert.All(steps.OfType<AccessStep.HashCompare>(), c => Assert.False(c.HasResidual));
        Assert.All(steps.OfType<AccessStep.HashCompare>(), c => Assert.False(c.ShowsResidual));

        Assert.All(steps.OfType<AccessStep.HashCompare>(), c => Assert.Equal(c.IsKeyMatch, c.IsMatch));
    }

    /// <summary>
    /// A step keeps the id of whatever produced it, rather than being restamped by the operator reading from it
    /// </summary>
    /// <remarks>
    /// This is what lets operators compose. A join that overwrote the source would credit a leaf's reads to whichever of its own inputs
    /// they arrived through, which is wrong the moment anything is nested more than one level deep.
    /// </remarks>
    [RequiresFileFact(MdfPath)]
    public async Task Assigned_Source_Ids_Follow_The_Operator_That_Produced_The_Step()
    {
        var context = await LoadNumberTableAsync();

        var build = SideInput(context.Unit, Between(100, 110), nodeId: 11);

        var probe = SideInput(context.Unit, Between(105, 120), nodeId: 22);

        var (steps, _) = await RunAsync(context, new HashMatchDefinition(build, probe) { NodeId = 33 });

        Assert.Equal([11, 22, 33], steps.Select(s => s.NodeId).Distinct().Order());

        Assert.All(steps.OfType<AccessStep.HashBuild>(), s => Assert.Equal(33, s.NodeId));
        Assert.All(steps.OfType<AccessStep.HashProbe>(), s => Assert.Equal(33, s.NodeId));
        Assert.All(steps.OfType<AccessStep.HashCompare>(), s => Assert.Equal(33, s.NodeId));
        Assert.All(steps.OfType<AccessStep.JoinEmit>(), s => Assert.Equal(33, s.NodeId));

        Assert.Contains(steps, s => s is AccessStep.Row { EmittedRecord: not null, NodeId: 11 });
        Assert.Contains(steps, s => s is AccessStep.Row { EmittedRecord: not null, NodeId: 22 });

        Assert.DoesNotContain(steps, s => s is AccessStep.Row && s.NodeId == 33);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Steps_Are_Attributed_And_Counters_Are_Combined()
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, Definition(context));

        Assert.Contains(steps, s => s.NodeId == 0);
        Assert.Contains(steps, s => s.NodeId == 1);
        Assert.Contains(steps, s => s.NodeId == 2);

        var pagesRead = steps.Select(s => s.Counters.PagesRead).ToList();

        Assert.Equal(pagesRead.OrderBy(p => p), pagesRead);
    }

    private sealed record NumberTableContext(DatabaseSource Database, HashMatchIterator Service, AllocationUnit Unit);

    private async Task<NumberTableContext> LoadNumberTableAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new NumberTableContext(database, serviceHost.GetService<HashMatchIterator>(), unit);
    }


    /// <summary>
    /// A hash match reading another hash match consumes the pairs it emitted, not the reads that produced them
    /// </summary>
    /// <remarks>
    /// The inner join's own leaves also report rows, so an operator that took any row step as its input would consume the inner join's
    /// build side instead of its results and pair against the wrong stream entirely.
    /// </remarks>
    [RequiresFileFact(MdfPath)]
    public async Task A_Hash_Match_Reads_The_Results_Of_A_Nested_Hash_Match()
    {
        var context = await LoadNumberTableAsync();

        var inner = new HashMatchDefinition(SideInput(context.Unit, Between(100, 110), 1),
                                            SideInput(context.Unit, Between(105, 120), 2))
        {
            NodeId = 3
        };

        var outer = new HashMatchDefinition(SideInput(context.Unit, Between(100, 130), 4),
                                            new JoinInputDefinition(inner, ["Id"]))
        {
            NodeId = 5
        };

        var (steps, _) = await RunAsync(context, outer);

        // The nested join's own emits pass through this stream too, so only the outer join's count as its output
        var emits = steps.OfType<AccessStep.JoinEmit>().Where(e => e.NodeId == 5).ToList();

        var nested = steps.OfType<AccessStep.JoinEmit>().Where(e => e.NodeId == 3).ToList();

        // The nested join pairs 105 to 110, and every one of those is inside the outer build's range
        Assert.Equal(6, nested.Count);
        Assert.Equal(6, emits.Count);

        Assert.Contains(steps, s => s.NodeId == 1);
        Assert.Contains(steps, s => s.NodeId == 2);
    }

    private static JoinInputDefinition SideInput(AllocationUnit unit, SeekBounds bounds, int nodeId)
        => new(new RangeDefinition(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = nodeId }, ["Id"]);

    private static HashMatchDefinition Definition(NumberTableContext context)
        => new(SideInput(context.Unit, Between(100, 110), 0), SideInput(context.Unit, Between(105, 120), 1)) { NodeId = 2 };

    private static Task<(List<AccessStep> Steps, List<(long Build, long Probe)> Pairs)> RunAsync(NumberTableContext context,
                                                                                                 IteratorDefinition definition)
        => RunAsync(new IteratorStepper(context.Service, definition, new IteratorContext(context.Database)));

    private static async Task<(List<AccessStep> Steps, List<(long Build, long Probe)> Pairs)> RunAsync(IteratorStepper stepper)
    {
        await using var _ = stepper;

        var steps = new List<AccessStep>();

        var pairs = new List<(long Build, long Probe)>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.JoinEmit { IsUnmatched: false, OuterRecord: { } build, InnerRecord: { } probe })
            {
                pairs.Add((new RecordRowValueSource(build).GetValue(-1, "Id").Numeric,
                           new RecordRowValueSource(probe).GetValue(-1, "Id").Numeric));
            }
        }

        return (steps, pairs);
    }

    /// <summary>
    /// Runs the standard overlapping ranges under a semi or anti-semi join, returning what reached the output
    /// </summary>
    /// <remarks>
    /// Build holds 100 to 110 and probe 105 to 120, so six rows pair, five build rows have no partner and ten probe rows have none.
    /// </remarks>
    private async Task<List<AccessStep.JoinEmit>> RunSemiAsync(JoinType joinType)
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, Definition(context) with { JoinType = joinType });

        return [.. steps.OfType<AccessStep.JoinEmit>()];
    }

    private static IEnumerable<long> BuildIds(IEnumerable<AccessStep.JoinEmit> emits)
        => emits.Select(e => new RecordRowValueSource(e.OuterRecord!).GetValue(-1, "Id").Numeric).Order();

    private static IEnumerable<long> ProbeIds(IEnumerable<AccessStep.JoinEmit> emits)
        => emits.Select(e => new RecordRowValueSource(e.InnerRecord!).GetValue(-1, "Id").Numeric).Order();

    private static AccessPredicate IdAtLeast(int value)
        => new AccessPredicate.Comparison(new AccessExpression.Column(-1, "Id"),
                                          ComparisonOperator.GreaterThanOrEqual,
                                          new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, value)));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private static SeekBounds BigIntBetween(long from, long to)
        => SeekBounds.Between(AccessKey.Create(AccessValue.FromInteger(SqlDbType.BigInt, from).WithColumnName("Id")),
                              AccessKey.Create(AccessValue.FromInteger(SqlDbType.BigInt, to).WithColumnName("Id")));

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));
}
