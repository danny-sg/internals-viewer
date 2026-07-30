using System.Data;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Execution.Services.Joins;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

public class MergeJoinStepServiceTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    private const string NumberTable = "NumberTable_Clustered";

    [RequiresFileFact(MdfPath)]
    public async Task Overlapping_Ranges_Emit_Matching_Pairs()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(100, 110)),
                                         SideInput(context.Unit, Between(105, 120)),
                                         CancellationToken.None);

        var (steps, pairs) = await RunAsync(context.Service);

        Assert.Equal(6, pairs.Count);
        Assert.Equal(6, context.Service.PairCount);
        Assert.Equal(Enumerable.Range(105, 6).Select(v => ((long)v, (long)v)), pairs);

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(MergeJoinStepService.JoinSource, stopped.Source);
        Assert.True(context.Service.IsComplete);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Disjoint_Ranges_Emit_Nothing()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(100, 105)),
                                         SideInput(context.Unit, Between(200, 205)),
                                         CancellationToken.None);

        var (steps, pairs) = await RunAsync(context.Service);

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

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(500, 520)),
                                         SideInput(context.Unit, Between(500, 520)),
                                         CancellationToken.None);

        var (steps, pairs) = await RunAsync(context.Service);

        Assert.Equal(21, pairs.Count);
        Assert.All(pairs, p => Assert.Equal(p.Outer, p.Inner));

        var matches = steps.OfType<AccessStep.MergeCompare>().Count(c => c.Comparison == 0);

        Assert.Equal(21, matches);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Steps_Are_Attributed_And_Counters_Are_Combined()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         SideInput(context.Unit, Between(100, 110)),
                                         SideInput(context.Unit, Between(105, 120)),
                                         CancellationToken.None);

        var (steps, _) = await RunAsync(context.Service);

        Assert.Contains(steps, s => s.Source == MergeJoinStepService.OuterSource);
        Assert.Contains(steps, s => s.Source == MergeJoinStepService.InnerSource);
        Assert.Contains(steps, s => s.Source == MergeJoinStepService.JoinSource);

        var pagesRead = steps.Select(s => s.Counters.PagesRead).ToList();

        Assert.Equal(pagesRead.OrderBy(p => p), pagesRead);

        Assert.Equal(steps[^1].Counters.RowsOutput, steps.Max(s => s.Counters.RowsOutput));
    }

    private sealed record NumberTableContext(DatabaseSource Database, MergeJoinStepService Service, AllocationUnit Unit);

    private async Task<NumberTableContext> LoadNumberTableAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = database.AllocationUnits.Values.Single(a => a.TableName == NumberTable
                                                               && a.AllocationUnitType == AllocationUnitType.InRowData);

        return new NumberTableContext(database, serviceHost.GetService<MergeJoinStepService>(), unit);
    }

    private static MergeJoinSideInput SideInput(AllocationUnit unit, SeekBounds bounds)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds], ["Id"]);

    private static async Task<(List<AccessStep> Steps, List<(long Outer, long Inner)> Pairs)> RunAsync(MergeJoinStepService service)
    {
        var steps = new List<AccessStep>();

        var pairs = new List<(long Outer, long Inner)>();

        while (await service.StepNextAsync(CancellationToken.None) is { } step)
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
