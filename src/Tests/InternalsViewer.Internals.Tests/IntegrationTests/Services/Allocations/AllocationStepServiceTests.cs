using System.Data;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.DataAccess.AccessPaths.Binding;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Services.Allocations;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Allocations;

public class AllocationStepServiceTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    private const string NumberTable = "NumberTable_Clustered";

    private const int NumberTableRowCount = 10_000;

    [RequiresFileFact(MdfPath)]
    public async Task Allocation_Scan_Reads_Every_Row()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         context.Unit.FirstIamPage,
                                         null,
                                         CancellationToken.None);

        var (steps, values) = await RunAsync(context.Service);

        Assert.Equal(NumberTableRowCount, values.Count);
        Assert.Equal(Enumerable.Range(1, NumberTableRowCount).Select(v => (long)v), values.Order());

        Assert.IsType<AccessStep.IamRead>(steps[0]);
        Assert.Contains(steps, s => s is AccessStep.PfsRead);
        Assert.Contains(steps, s => s is AccessStep.ExtentStart);

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(StopReason.IndexExhausted, stopped.Reason);
        Assert.Equal(NumberTableRowCount, stopped.Counters.RowsOutput);
        Assert.Equal(1, stopped.Counters.IamPagesRead);
        Assert.True(stopped.Counters.PfsPagesRead > 0);
        Assert.True(stopped.Counters.ExtentsVisited > 0);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Index_Pages_Are_Read_And_Skipped()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         context.Unit.FirstIamPage,
                                         null,
                                         CancellationToken.None);

        var (steps, _) = await RunAsync(context.Service);

        var skipped = steps.OfType<AccessStep.PageSkipped>().ToList();

        Assert.Contains(skipped, s => s.Reason == PageSkipReason.IndexPage);
        Assert.Equal(skipped.Count, steps[^1].Counters.PagesSkipped);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Residual_Filters_Rows()
    {
        var context = await LoadNumberTableAsync();

        var residual = new AccessPredicate.Comparison(
            new AccessExpression.Arithmetic(ArithmeticOperator.Modulo,
                                            new AccessExpression.Column(-1, "Id"),
                                            new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, 2))),
            ComparisonOperator.Equal,
            new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, 0)));

        await context.Service.StartAsync(context.Database,
                                         context.Unit.FirstIamPage,
                                         residual,
                                         CancellationToken.None);

        var (steps, values) = await RunAsync(context.Service);

        Assert.Equal(NumberTableRowCount / 2, values.Count);
        Assert.All(values, v => Assert.Equal(0, v % 2));

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(NumberTableRowCount, stopped.Counters.RowsRead);
        Assert.Equal(NumberTableRowCount / 2, stopped.Counters.RowsOutput);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Row_Goal_Stops_The_Scan()
    {
        var context = await LoadNumberTableAsync();

        await context.Service.StartAsync(context.Database,
                                         context.Unit.FirstIamPage,
                                         null,
                                         CancellationToken.None,
                                         rowGoal: 25);

        var (steps, values) = await RunAsync(context.Service);

        Assert.Equal(25, values.Count);

        var stopped = Assert.IsType<AccessStep.Stopped>(steps[^1]);

        Assert.Equal(StopReason.RowGoalMet, stopped.Reason);
        Assert.Equal(25, stopped.Counters.RowsOutput);
    }

    private sealed record NumberTableContext(DatabaseSource Database, AllocationStepService Service, AllocationUnit Unit);

    private async Task<NumberTableContext> LoadNumberTableAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = database.AllocationUnits.Values.Single(a => a.TableName == NumberTable
                                                               && a.AllocationUnitType == AllocationUnitType.InRowData);

        return new NumberTableContext(database, serviceHost.GetService<AllocationStepService>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<long> Values)> RunAsync(AllocationStepService service)
    {
        var steps = new List<AccessStep>();

        var values = new List<long>();

        while (await service.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.Row { EmittedRecord: { } record })
            {
                values.Add(new RecordRowValueSource(record).GetValue(-1, "Id").Numeric);
            }
        }

        return (steps, values);
    }
}
