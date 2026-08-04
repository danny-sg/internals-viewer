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
using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

public class NestedLoopsIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    private const int OuterNodeId = 0;

    private const int InnerNodeId = 1;

    private const int JoinNodeId = 2;

    [RequiresFileFact(MdfPath)]
    public async Task Rebinds_Once_Per_Outer_Row()
    {
        var context = await LoadNumberTableAsync();

        var definition = Join(OuterInput(context.Unit, Between(100, 110)), InnerInput(context.Unit));

        var (steps, innerValues) = await RunAsync(context, definition);

        var rebinds = steps.OfType<AccessStep.Rebind>().ToList();

        Assert.Equal(11, rebinds.Count);
        Assert.Equal(11, context.Service.RebindCount);

        Assert.Equal(Enumerable.Range(100, 11).Select(v => (long)v), rebinds.Select(r => r.Key[0].Numeric));
        Assert.Equal(Enumerable.Range(100, 11).Select(v => (long)v), innerValues);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Each_Rebind_Descends_From_The_Inner_Root()
    {
        var context = await LoadNumberTableAsync();

        var definition = Join(OuterInput(context.Unit, Between(100, 105)), InnerInput(context.Unit));

        var (steps, _) = await RunAsync(context, definition);

        foreach (var rebind in steps.OfType<AccessStep.Rebind>())
        {
            var next = steps[steps.IndexOf(rebind) + 1];

            var readPage = Assert.IsType<AccessStep.ReadPage>(next);

            Assert.Equal(context.Unit.RootPage, readPage.PageAddress);
            Assert.Equal(InnerNodeId, readPage.NodeId);
        }
    }

    [RequiresFileFact(MdfPath)]
    public async Task Unique_Inner_Seek_Emits_One_Row_Per_Rebind()
    {
        var context = await LoadNumberTableAsync();

        var definition = Join(OuterInput(context.Unit, Between(500, 520)), InnerInput(context.Unit));

        var (steps, innerValues) = await RunAsync(context, definition);

        Assert.Equal(21, innerValues.Count);

        var perRebind = new List<int>();

        var current = -1;

        foreach (var step in steps)
        {
            if (step is AccessStep.Rebind)
            {
                perRebind.Add(0);
                current = perRebind.Count - 1;
            }
            else if (step is AccessStep.Row { EmittedRecord: not null, NodeId: InnerNodeId })
            {
                perRebind[current]++;
            }
        }

        Assert.Equal(21, perRebind.Count);
        Assert.All(perRebind, count => Assert.Equal(1, count));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Steps_Are_Attributed_To_Nodes_And_Counters_Are_Combined()
    {
        var context = await LoadNumberTableAsync();

        var definition = Join(OuterInput(context.Unit, Between(100, 110)), InnerInput(context.Unit));

        var (steps, _) = await RunAsync(context, definition);

        Assert.Contains(steps, s => s.NodeId == OuterNodeId);
        Assert.Contains(steps, s => s.NodeId == InnerNodeId);

        var pagesRead = steps.Select(s => s.Counters.PagesRead).ToList();

        Assert.Equal(pagesRead.OrderBy(p => p), pagesRead);

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(JoinNodeId, stopped.NodeId);
        Assert.Equal(StopReason.RangeEnded, stopped.Reason);
        Assert.True(context.Service.IsComplete);

        Assert.Equal(11 + 11, stopped.Counters.RowsOutput);
        Assert.Equal(1 + 11, stopped.Counters.RangeSeeks);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Missing_Correlated_Column_Throws()
    {
        var context = await LoadNumberTableAsync();

        var innerInput = new SeekDefinition(context.Unit.AllocationUnitId,
                                            context.Unit.RootPage,
                                            [new CorrelationBinding("Id", "NoSuchColumn")]) { NodeId = InnerNodeId };

        var definition = Join(OuterInput(context.Unit, Between(100, 100)), innerInput);

        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            while (await stepper.StepNextAsync(CancellationToken.None) is not null)
            {
            }
        });
    }

    private sealed record NumberTableContext(DatabaseSource Database, NestedLoopsIterator Service, AllocationUnit Unit);

    private async Task<NumberTableContext> LoadNumberTableAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new NumberTableContext(database, serviceHost.GetService<NestedLoopsIterator>(), unit);
    }

    private static NestedLoopsDefinition Join(IteratorDefinition outer, IteratorDefinition inner)
        => new(outer, inner) { NodeId = JoinNodeId };

    private static RangeDefinition OuterInput(AllocationUnit unit, SeekBounds bounds)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = OuterNodeId };

    private static SeekDefinition InnerInput(AllocationUnit unit)
        => new(unit.AllocationUnitId, unit.RootPage, [new CorrelationBinding("Id", "Id")]) { NodeId = InnerNodeId };

    private static async Task<(List<AccessStep> Steps, List<long> InnerValues)> RunAsync(NumberTableContext context,
                                                                                         IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var innerValues = new List<long>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.Row { EmittedRecord: { } record, NodeId: InnerNodeId })
            {
                innerValues.Add(new RecordRowValueSource(record).GetValue(-1, "Id").Numeric);
            }
        }

        return (steps, innerValues);
    }

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));
}
