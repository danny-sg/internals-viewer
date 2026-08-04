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
using InternalsViewer.Internals.Tests.Helpers;

using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.Iterators.DataAccess;
using InternalsViewer.Execution.Iterators.Stepping;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Indexes;

public class IndexIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    private const int NumberTableRowCount = DemoDatabase.ClusteredTableRowCount;

    [RequiresFileFact(MdfPath)]
    public async Task Scan_Reads_All_Rows_In_Order()
    {
        var context = await LoadNumberTableAsync();

        var definition = new RangeDefinition(context.Unit.AllocationUnitId, context.Unit.RootPage, [SeekBounds.All]);

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal(NumberTableRowCount, values.Count);
        Assert.Equal(1, values[0]);
        Assert.Equal(NumberTableRowCount, values[^1]);
        Assert.True(values.Zip(values.Skip(1)).All(p => p.Second == p.First + 1));

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.PageExhausted, stopped.Reason);
        Assert.Equal(NumberTableRowCount, stopped.Counters.RowsOutput);
        Assert.Equal(NumberTableRowCount, stopped.Counters.RowsRead);
        Assert.True(stopped.Counters.LeafLinksFollowed > 0);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Seek_Emits_Exactly_The_Range()
    {
        var context = await LoadNumberTableAsync();

        var definition = new RangeDefinition(context.Unit.AllocationUnitId, context.Unit.RootPage, [Between(1000, 1200)]);

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal(201, values.Count);
        Assert.Equal(1000, values[0]);
        Assert.Equal(1200, values[^1]);

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.RangeEnded, stopped.Reason);
        Assert.Equal(201, stopped.Counters.RowsOutput);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Backward_Seek_Emits_The_Range_In_Reverse()
    {
        var context = await LoadNumberTableAsync();

        var definition = new RangeDefinition(context.Unit.AllocationUnitId, context.Unit.RootPage, [Between(1000, 1200)])
        {
            Direction = ScanDirection.Backward
        };

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal(201, values.Count);
        Assert.Equal(1200, values[0]);
        Assert.Equal(1000, values[^1]);
        Assert.True(values.Zip(values.Skip(1)).All(p => p.Second == p.First - 1));

        Assert.Contains(steps, s => s is AccessStep.LeafLink { Direction: ScanDirection.Backward });

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.RangeEnded, stopped.Reason);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Multiple_Ranges_Reseek_From_The_Root()
    {
        var context = await LoadNumberTableAsync();

        var definition = new RangeDefinition(context.Unit.AllocationUnitId,
                                             context.Unit.RootPage,
                                             [Between(100, 110), Between(5000, 5010), Between(9990, 10000)]);

        var (steps, values) = await RunAsync(context, definition);

        var expected = Enumerable.Range(100, 11)
                                 .Concat(Enumerable.Range(5000, 11))
                                 .Concat(Enumerable.Range(9990, 11))
                                 .Select(v => (long)v);

        Assert.Equal(expected, values);

        var reseeks = steps.OfType<AccessStep.Reseek>().ToList();

        Assert.Equal(2, reseeks.Count);
        Assert.Equal(3, steps[^1].Counters.RangeSeeks);

        foreach (var reseek in reseeks)
        {
            var next = steps[steps.IndexOf(reseek) + 1];

            var readPage = Assert.IsType<AccessStep.ReadPage>(next);

            Assert.Equal(context.Unit.RootPage, readPage.PageAddress);
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

        var definition = new RangeDefinition(context.Unit.AllocationUnitId, context.Unit.RootPage, [Between(1, 100)])
        {
            Residual = residual
        };

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal(50, values.Count);
        Assert.All(values, v => Assert.Equal(0, v % 2));

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(100, stopped.Counters.RowsRead);
        Assert.Equal(50, stopped.Counters.RowsOutput);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Row_Goal_Stops_The_Walk()
    {
        var context = await LoadNumberTableAsync();

        var definition = new RangeDefinition(context.Unit.AllocationUnitId, context.Unit.RootPage, [Between(500, 9000)])
        {
            RowGoal = 25
        };

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal(25, values.Count);
        Assert.Equal(500, values[0]);
        Assert.Equal(524, values[^1]);

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.RowGoalMet, stopped.Reason);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Unbounded_End_Runs_Off_The_Index()
    {
        var context = await LoadNumberTableAsync();

        var bounds = new SeekBounds
        {
            StartValue = Key(NumberTableRowCount - 10),
            IsStartInclusive = true,
            CompareWidth = 1
        };

        var definition = new RangeDefinition(context.Unit.AllocationUnitId, context.Unit.RootPage, [bounds]);

        var (steps, values) = await RunAsync(context, definition);

        Assert.Equal(11, values.Count);
        Assert.Equal(NumberTableRowCount - 10, values[0]);
        Assert.Equal(NumberTableRowCount, values[^1]);

        var stopped = steps.OfType<AccessStep.Stopped>().Last();

        Assert.Equal(StopReason.PageExhausted, stopped.Reason);
    }

    private sealed record NumberTableContext(DatabaseSource Database, IndexIterator Service, AllocationUnit Unit);

    private async Task<NumberTableContext> LoadNumberTableAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new NumberTableContext(database, serviceHost.GetService<IndexIterator>(), unit);
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

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));
}
