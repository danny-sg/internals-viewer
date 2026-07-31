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
using InternalsViewer.Execution.Services.Joins;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

public class NestedLoopsStepServiceTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

        [RequiresFileFact(MdfPath)]
    public async Task Rebinds_Once_Per_Outer_Row()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         OuterInput(context.Unit, Between(100, 110)),
                                         InnerInput(context.Unit),
                                         CancellationToken.None);

        var (steps, innerValues) = await RunAsync(context.Service);

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

        await context.Service.StartAsync(context.Database,
                                         OuterInput(context.Unit, Between(100, 105)),
                                         InnerInput(context.Unit),
                                         CancellationToken.None);

        var (steps, _) = await RunAsync(context.Service);

        foreach (var rebind in steps.OfType<AccessStep.Rebind>())
        {
            var next = steps[steps.IndexOf(rebind) + 1];

            var readPage = Assert.IsType<AccessStep.ReadPage>(next);

            Assert.Equal(context.Unit.RootPage, readPage.PageAddress);
            Assert.Equal(NestedLoopsStepService.InnerSource, readPage.Source);
        }
    }

    [RequiresFileFact(MdfPath)]
    public async Task Unique_Inner_Seek_Emits_One_Row_Per_Rebind()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         OuterInput(context.Unit, Between(500, 520)),
                                         InnerInput(context.Unit),
                                         CancellationToken.None);

        var (steps, innerValues) = await RunAsync(context.Service);

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
            else if (step is AccessStep.Row { EmittedRecord: not null, Source: NestedLoopsStepService.InnerSource })
            {
                perRebind[current]++;
            }
        }

        Assert.Equal(21, perRebind.Count);
        Assert.All(perRebind, count => Assert.Equal(1, count));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Steps_Are_Attributed_To_Sides_And_Counters_Are_Combined()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         OuterInput(context.Unit, Between(100, 110)),
                                         InnerInput(context.Unit),
                                         CancellationToken.None);

        var (steps, _) = await RunAsync(context.Service);

        Assert.Contains(steps, s => s.Source == NestedLoopsStepService.OuterSource);
        Assert.Contains(steps, s => s.Source == NestedLoopsStepService.InnerSource);

        var pagesRead = steps.Select(s => s.Counters.PagesRead).ToList();

        Assert.Equal(pagesRead.OrderBy(p => p), pagesRead);

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(NestedLoopsStepService.OuterSource, stopped.Source);
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
                                                   [new CorrelationBinding("Id", "NoSuchColumn")]);

        await context.Service.StartAsync(context.Database,
                                         OuterInput(context.Unit, Between(100, 100)),
                                         innerInput,
                                         CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            while (await context.Service.StepNextAsync(CancellationToken.None) is not null)
            {
            }
        });
    }

    private sealed record NumberTableContext(DatabaseSource Database, NestedLoopsStepService Service, AllocationUnit Unit);

    private async Task<NumberTableContext> LoadNumberTableAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new NumberTableContext(database, serviceHost.GetService<NestedLoopsStepService>(), unit);
    }

    private static RangeDefinition OuterInput(AllocationUnit unit, SeekBounds bounds)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]);

    private static SeekDefinition InnerInput(AllocationUnit unit)
        => new(unit.AllocationUnitId, unit.RootPage, [new CorrelationBinding("Id", "Id")]);

    private static async Task<(List<AccessStep> Steps, List<long> InnerValues)> RunAsync(NestedLoopsStepService service)
    {
        var steps = new List<AccessStep>();

        var innerValues = new List<long>();

        while (await service.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.Row { EmittedRecord: { } record, Source: NestedLoopsStepService.InnerSource })
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
