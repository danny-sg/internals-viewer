using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

/// <summary>
/// A merge join of two different tables, the heap's index on Id against the clustered table, both walked in Id order
/// </summary>
/// <remarks>
/// HeapTable holds every twentieth Id of ClusteredTable, so the clustered side has to walk nineteen rows between matches. That makes the
/// catch up behaviour of a merge join measurable rather than incidental.
/// </remarks>
public class MergeJoinAcrossTablesTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Sparse_Outer_Matches_Every_Twentieth_Clustered_Row()
    {
        var context = await LoadAsync();

        await StartAsync(context, from: 100, to: 200);

        var (_, pairs) = await RunAsync(context.Service);

        Assert.Equal(Enumerable.Range(5, 6).Select(v => (long?)(v * 20)), pairs.Select(p => p.Outer));
        Assert.All(pairs, p => Assert.Equal(p.Outer, p.Inner));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Dense_Side_Reads_Far_More_Rows_Than_It_Matches()
    {
        var context = await LoadAsync();

        await StartAsync(context, from: 100, to: 200);

        var (steps, pairs) = await RunAsync(context.Service);

        var outerRows = CountRows(steps, MergeJoinStepIterator.OuterSource);

        var innerRows = CountRows(steps, MergeJoinStepIterator.InnerSource);

        TestOutput.WriteLine($"{pairs.Count} pairs from {outerRows} heap index rows and {innerRows} clustered rows");

        Assert.Equal(6, pairs.Count);
        Assert.True(innerRows > outerRows * 15, "The clustered side has to walk the gaps between the heap's keys");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Side_That_Is_Behind_Is_The_Side_That_Advances()
    {
        var context = await LoadAsync();

        await StartAsync(context, from: 100, to: 200);

        var (steps, _) = await RunAsync(context.Service);

        var compares = steps.OfType<AccessStep.MergeCompare>().Where(c => c.Comparison != 0).ToList();

        Assert.NotEmpty(compares);

        // Only the clustered side is ever behind, because it holds every key the heap index holds
        Assert.All(compares, c => Assert.True(c.Comparison > 0, $"Unexpected outer advance at {c.OuterKey} vs {c.InnerKey}"));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Left_Outer_Join_Returns_Clustered_Rows_With_No_Heap_Row()
    {
        var context = await LoadAsync();

        // The clustered table drives, so every row it holds is returned whether or not the heap has it
        await StartAsync(context, from: 100, to: 200, joinType: JoinType.LeftOuter, isClusteredOuter: true);

        var (_, pairs) = await RunAsync(context.Service);

        Assert.Equal(101, pairs.Count);
        Assert.Equal(6, pairs.Count(p => p.Inner is not null));
        Assert.Equal(95, pairs.Count(p => p.Inner is null));
    }

    private static int CountRows(List<AccessStep> steps, int source)
        => steps.Count(s => s is AccessStep.Row { EmittedRecord: not null } && s.Source == source);

    private sealed record Context(DatabaseSource Database,
                                  MergeJoinStepIterator Service,
                                  AllocationUnit HeapIndex,
                                  AllocationUnit Clustered);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        return new Context(database,
                           serviceHost.GetService<MergeJoinStepIterator>(),
                           DemoDatabase.Unit(database, DemoDatabase.HeapTable, DemoDatabase.HeapIndex),
                           DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex));
    }

    private static async Task StartAsync(Context context,
                                         int from,
                                         int to,
                                         JoinType joinType = JoinType.Inner,
                                         bool isClusteredOuter = false)
    {
        var heap = SideInput(context.HeapIndex, from, to);

        var clustered = SideInput(context.Clustered, from, to);

        await context.Service.OpenAsync(new IteratorContext(context.Database),
                                        new MergeJoinDefinition(isClusteredOuter ? clustered : heap, isClusteredOuter ? heap : clustered)
        {
            JoinType = joinType
        },
                                        CancellationToken.None);
    }

    private static MergeSideDefinition SideInput(AllocationUnit unit, int from, int to)
        => new(unit.AllocationUnitId, unit.RootPage, [Between(from, to)], ["Id"]);

    private static async Task<(List<AccessStep> Steps, List<(long? Outer, long? Inner)> Pairs)> RunAsync(MergeJoinStepIterator service)
    {
        var steps = new List<AccessStep>();

        var pairs = new List<(long? Outer, long? Inner)>();

        while (await service.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.JoinEmit emit)
            {
                pairs.Add((Value(emit.OuterRecord), Value(emit.InnerRecord)));
            }
        }

        return (steps, pairs);
    }

    private static long? Value(IRecord? record)
        => record is null ? null : new RecordRowValueSource(record).GetValue(-1, "Id").Numeric;

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));
}
