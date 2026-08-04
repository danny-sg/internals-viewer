using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using System.Data;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

public class MergeJoinIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

        [RequiresFileFact(MdfPath)]
    public async Task Overlapping_Ranges_Emit_Matching_Pairs()
    {
        var context = await LoadNumberTableAsync();

        var definition = new MergeJoinDefinition(SideInput(context.Unit, Between(100, 110), 0),
                                                 SideInput(context.Unit, Between(105, 120), 1)) { NodeId = 2 };

        var (steps, pairs) = await RunAsync(context, definition);

        Assert.Equal(6, pairs.Count);
        Assert.Equal(6, context.Service.PairCount);
        Assert.Equal(Enumerable.Range(105, 6).Select(v => ((long)v, (long)v)), pairs);

        var outerRows = steps.Count(s => s is AccessStep.Row { EmittedRecord: not null, NodeId: 0 });

        var innerRows = steps.Count(s => s is AccessStep.Row { EmittedRecord: not null, NodeId: 1 });

        Assert.Equal(11, outerRows);
        Assert.Equal(7, innerRows);

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(2, stopped.NodeId);
        Assert.True(context.Service.IsComplete);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Disjoint_Ranges_Emit_Nothing()
    {
        var context = await LoadNumberTableAsync();

        var definition = new MergeJoinDefinition(SideInput(context.Unit, Between(100, 105), 0),
                                                 SideInput(context.Unit, Between(200, 205), 1)) { NodeId = 2 };

        var (steps, pairs) = await RunAsync(context, definition);

        Assert.Empty(pairs);

        var compares = steps.OfType<AccessStep.MergeCompare>().ToList();

        Assert.NotEmpty(compares);
        Assert.All(compares, c => Assert.True(c.Comparison < 0));

        Assert.IsType<AccessStep.Stopped>(steps[^1]);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Identical_Ranges_Pair_Every_Row()
    {
        var context = await LoadNumberTableAsync();

        var definition = new MergeJoinDefinition(SideInput(context.Unit, Between(500, 520), 0),
                                                 SideInput(context.Unit, Between(500, 520), 1)) { NodeId = 2 };

        var (steps, pairs) = await RunAsync(context, definition);

        Assert.Equal(21, pairs.Count);
        Assert.All(pairs, p => Assert.Equal(p.Outer, p.Inner));

        var matches = steps.OfType<AccessStep.MergeCompare>().Count(c => c.Comparison == 0);

        Assert.Equal(21, matches);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Steps_Are_Attributed_And_Counters_Are_Combined()
    {
        var context = await LoadNumberTableAsync();

        var definition = new MergeJoinDefinition(SideInput(context.Unit, Between(100, 110), 0),
                                                 SideInput(context.Unit, Between(105, 120), 1)) { NodeId = 2 };

        var (steps, _) = await RunAsync(context, definition);

        Assert.Contains(steps, s => s.NodeId == 0);
        Assert.Contains(steps, s => s.NodeId == 1);
        Assert.Contains(steps, s => s.NodeId == 2);

        var pagesRead = steps.Select(s => s.Counters.PagesRead).ToList();

        Assert.Equal(pagesRead.OrderBy(p => p), pagesRead);

        Assert.Equal(steps[^1].Counters.RowsOutput, steps.Max(s => s.Counters.RowsOutput));
    }

    private sealed record NumberTableContext(DatabaseSource Database, MergeJoinIterator Service, AllocationUnit Unit);

    private async Task<NumberTableContext> LoadNumberTableAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new NumberTableContext(database, serviceHost.GetService<MergeJoinIterator>(), unit);
    }

    private static JoinInputDefinition SideInput(AllocationUnit unit, SeekBounds bounds, int nodeId)
        => new(new RangeDefinition(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = nodeId }, ["Id"]);

    private static async Task<(List<AccessStep> Steps, List<(long Outer, long Inner)> Pairs)> RunAsync(NumberTableContext context,
                                                                                                       IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var pairs = new List<(long Outer, long Inner)>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.JoinEmit { OuterRecord: { } outer, InnerRecord: { } inner })
            {
                pairs.Add((new RecordRowValueSource(outer).GetValue(-1, "Id").Numeric,
                           new RecordRowValueSource(inner).GetValue(-1, "Id").Numeric));
            }
        }

        return (steps, pairs);
    }

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));
}
