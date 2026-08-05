using System.Data;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Iterators.DataAccess;
using InternalsViewer.Execution.Iterators.Stepping;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Allocations;

public class AllocationScanIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

        private const int NumberTableRowCount = DemoDatabase.ClusteredTableRowCount;

    [RequiresFileFact(MdfPath)]
    public async Task Allocation_Scan_Reads_Every_Row()
    {
        var context = await LoadNumberTableAsync();

        var (steps, values) = await RunAsync(context, new AllocationScanDefinition(context.Unit.FirstIamPage));

        Assert.Equal(NumberTableRowCount, values.Count);
        Assert.Equal(Enumerable.Range(1, NumberTableRowCount).Select(v => (long)v), values.Order());

        Assert.IsType<AccessStep.Open>(steps[0]);
        Assert.IsType<AccessStep.IamRead>(steps[1]);
        Assert.Contains(steps, s => s is AccessStep.PfsRead);
        Assert.Contains(steps, s => s is AccessStep.ExtentStart);

        var pfsChecks = steps.Count(s => s is AccessStep.PfsCheck);

        var pagesVisited = steps.Count(s => s is AccessStep.ReadPage)
                           + steps.Count(s => s is AccessStep.PageSkipped { Reason: PageSkipReason.NotAllocated });

        Assert.Equal(pagesVisited, pfsChecks);

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.AllocationExhausted, stopped.Reason);
        Assert.Equal(NumberTableRowCount, stopped.Counters.RowsOutput);
        Assert.Equal(1, stopped.Counters.IamPagesRead);
        Assert.True(stopped.Counters.PfsPagesRead > 0);
        Assert.True(stopped.Counters.ExtentsVisited > 0);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Index_Pages_Are_Read_And_Skipped()
    {
        var context = await LoadNumberTableAsync();

        var (steps, _) = await RunAsync(context, new AllocationScanDefinition(context.Unit.FirstIamPage));

        var skipped = steps.OfType<AccessStep.PageSkipped>().ToList();

        Assert.Contains(skipped, s => s.Reason == PageSkipReason.IndexPage);
        Assert.Equal(skipped.Count, steps[^1].Counters.PagesSkipped);

        foreach (var skip in skipped.Where(s => s.Reason == PageSkipReason.IndexPage))
        {
            var previous = steps[steps.IndexOf(skip) - 1];

            var read = Assert.IsType<AccessStep.ReadPage>(previous);

            Assert.Equal(skip.PageAddress, read.PageAddress);
            Assert.True(read.Level > 0);
        }
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

        var (steps, values) = await RunAsync(context, new AllocationScanDefinition(context.Unit.FirstIamPage)
        {
            Residual = residual
        });

        Assert.Equal(NumberTableRowCount / 2, values.Count);
        Assert.All(values, v => Assert.Equal(0, v % 2));

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(NumberTableRowCount, stopped.Counters.RowsRead);
        Assert.Equal(NumberTableRowCount / 2, stopped.Counters.RowsOutput);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Row_Goal_Stops_The_Scan()
    {
        var context = await LoadNumberTableAsync();

        var (steps, values) = await RunAsync(context, new AllocationScanDefinition(context.Unit.FirstIamPage)
        {
            RowGoal = 25
        });

        Assert.Equal(25, values.Count);

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.RowGoalMet, stopped.Reason);
        Assert.Equal(25, stopped.Counters.RowsOutput);
    }

    private sealed record NumberTableContext(DatabaseSource Database, AllocationScanIterator Service, AllocationUnit Unit);

    private async Task<NumberTableContext> LoadNumberTableAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new NumberTableContext(database, serviceHost.GetService<AllocationScanIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<long> Values)> RunAsync(NumberTableContext context,
                                                                                    IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var values = new List<long>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
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
