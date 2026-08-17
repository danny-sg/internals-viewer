using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Execution.Iterators.Windowing;
using InternalsViewer.Execution.Tests.Helpers;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Windowing;

public class SegmentIteratorTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Every_Row_Is_Passed_On_Carrying_The_Flag()
    {
        var context = await LoadAsync();

        var definition = new SegmentDefinition(Bucketed(context.Unit, 100, 114), "Segment1003")
        {
            NodeId = 2,
            GroupBy = ["Bucket"]
        };

        var (steps, rows) = await RunAsync(context, definition);

        Assert.Equal(15, rows.Count);

        Assert.Equal(Enumerable.Range(100, 15).Select(i => (long)i), rows.Select(r => Value(r, "Id").Numeric));

        Assert.All(rows, r => Assert.False(Value(r, "Segment1003").IsNull));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Flag_Is_Set_On_The_First_Row_Of_Each_Group()
    {
        var context = await LoadAsync();

        var definition = new SegmentDefinition(Bucketed(context.Unit, 100, 114), "Segment1003")
        {
            NodeId = 2,
            GroupBy = ["Bucket"]
        };

        var (steps, rows) = await RunAsync(context, definition);

        var flagged = steps.OfType<AccessStep.SegmentRow>().Where(s => s.IsNewSegment).Select(s => s.Number).ToList();

        // Id 100 to 114 buckets into 20, 21 and 22, five rows each
        Assert.Equal([1, 6, 11], flagged);

        Assert.Equal(3, steps.OfType<AccessStep.SegmentRow>().Last().SegmentCount);

        Assert.Equal([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0],
                     rows.Select(r => Value(r, "Segment1003").Numeric));

        TestOutput.WriteLine($"{steps.Count} steps to return {rows.Count} rows");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task No_Grouping_Columns_Make_The_Whole_Input_One_Segment()
    {
        var context = await LoadAsync();

        var definition = new SegmentDefinition(Bucketed(context.Unit, 100, 114), "Segment1003") { NodeId = 2 };

        var (steps, rows) = await RunAsync(context, definition);

        Assert.Equal(15, rows.Count);

        var flagged = steps.OfType<AccessStep.SegmentRow>().Where(s => s.IsNewSegment).Select(s => s.Number).ToList();

        Assert.Equal([1], flagged);

        Assert.Equal(1, steps.OfType<AccessStep.SegmentRow>().Last().SegmentCount);
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Grouping_Column_The_Row_Does_Not_Have_Is_An_Error()
    {
        var context = await LoadAsync();

        var definition = new SegmentDefinition(Range(context.Unit, Between(100, 104)), "Segment1003")
        {
            NodeId = 2,
            GroupBy = ["NotAColumn"]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(context, definition));
    }

    private static ComputeScalarDefinition Bucketed(AllocationUnit unit, int from, int to)
        => new(Range(unit, Between(from, to)))
        {
            NodeId = 1,
            Columns = [new ComputedColumn("Bucket", Divide(Column("Id"), 5)) { DataType = SqlDbType.Int }]
        };

    private static AccessExpression Divide(AccessExpression left, int divisor)
        => new AccessExpression.Arithmetic(ArithmeticOperator.Divide,
                                           left,
                                           new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, divisor)));

    private static AccessExpression Column(string name)
        => new AccessExpression.Column(-1, name);

    private static AccessValue Value(IRecord row, string column)
        => new RecordRowValueSource(row).GetValue(-1, column);

    private static RangeDefinition Range(AllocationUnit unit, SeekBounds bounds)
        => new(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = 0 };

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));

    private sealed record Context(DatabaseSource Database, SegmentIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<SegmentIterator>(), unit);
    }

    private static async Task<(List<AccessStep> Steps, List<IRecord> Rows)> RunAsync(Context context, IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var rows = new List<IRecord>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.SegmentRow { EmittedRecord: { } record })
            {
                rows.Add(record);
            }
        }

        return (steps, rows);
    }
}
