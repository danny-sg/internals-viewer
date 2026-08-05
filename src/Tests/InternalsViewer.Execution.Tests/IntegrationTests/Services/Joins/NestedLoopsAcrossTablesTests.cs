using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Execution.Iterators.Stepping;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

/// <summary>
/// A nested loops join between two different tables, where the inner side is a correlated seek of another object rather than a lookup
/// back into the row the outer side came from
/// </summary>
/// <remarks>
/// HeapTable holds every twentieth Id of ClusteredTable, so every outer row has exactly one partner and the join is driven by the
/// smaller side.
/// </remarks>
public class NestedLoopsAcrossTablesTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Bound_Column_Joins_Two_Separate_Tables()
    {
        var context = await LoadAsync();

        var (_, emits) = await RunAsync(context, Definition(context, Between(500, 600)));

        Assert.NotEmpty(emits);

        foreach (var emit in emits)
        {
            Assert.Equal(Value(emit.OuterRecord, "Id"), Value(emit.InnerRecord, "Id"));
        }

        // The heap holds every twentieth Id, so this range covers the six multiples of twenty from 500 to 600
        Assert.Equal(Enumerable.Range(25, 6).Select(v => (long)(v * 20)), emits.Select(e => Value(e.OuterRecord, "Id")));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task The_Inner_Row_Comes_From_The_Other_Table()
    {
        var context = await LoadAsync();

        // 500 is the only multiple of twenty in this range, so the heap contributes exactly one row
        var (_, emits) = await RunAsync(context, Definition(context, Between(500, 510)));

        var emit = Assert.Single(emits);

        // CreatedDate exists only on ClusteredTable, FixedTextField only on HeapTable, so each side brought its own columns
        Assert.Contains(emit.InnerRecord!.Fields, f => f.Name == "CreatedDate");
        Assert.DoesNotContain(emit.OuterRecord!.Fields, f => f.Name == "CreatedDate");

        TestOutput.WriteLine($"outer: {string.Join(", ", emit.OuterRecord.Fields.Select(f => f.Name))}");
        TestOutput.WriteLine($"inner: {string.Join(", ", emit.InnerRecord.Fields.Select(f => f.Name))}");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Each_Rebind_Descends_The_Inner_Tables_Own_Root()
    {
        var context = await LoadAsync();

        var (steps, emits) = await RunAsync(context, Definition(context, Between(500, 600)));

        var rebinds = steps.OfType<AccessStep.Rebind>().ToList();

        Assert.Equal(emits.Count, rebinds.Count);

        foreach (var rebind in rebinds)
        {
            var read = Assert.IsType<AccessStep.ReadPage>(steps[steps.IndexOf(rebind) + 1]);

            // A lookup would re-enter the outer's own object, so reading the other table's root is what makes this a join of two tables
            Assert.Equal(context.Inner.RootPage, read.PageAddress);
            Assert.NotEqual(context.Outer.RootPage, read.PageAddress);
        }

        TestOutput.WriteLine($"{rebinds.Count} rebinds, each descending {context.Inner.RootPage}");
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task An_Outer_Row_With_No_Partner_Is_Dropped_By_An_Inner_Join()
    {
        var context = await LoadAsync();

        // ClusteredTable stops at 100,000, so the last heap rows still match and nothing is dropped on an inner join
        var (steps, emits) = await RunAsync(context, Definition(context, Between(99_960, 100_000), joinType: JoinType.Inner));

        Assert.Equal(emits.Count, steps.OfType<AccessStep.Rebind>().Count());
        Assert.All(emits, e => Assert.False(e.IsUnmatched));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Residual_Reading_A_Column_Only_The_Outer_Row_Has_Is_Refused()
    {
        var context = await LoadAsync();

        // RID is the row identifier the heap's index carries in place of a clustered key, so ClusteredTable has no such column to read
        var residual = new AccessPredicate.IsNull(new AccessExpression.Column(-1, "RID"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await RunAsync(context, Definition(context, Between(500, 600), residual)));

        Assert.Contains("'RID'", error.Message);

        TestOutput.WriteLine(error.Message);
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Residual_Reading_A_Name_Both_Rows_Have_Is_Left_Alone()
    {
        var context = await LoadAsync();

        // Id is on both sides, and a residual means the inner row, so this is the seek's own filter rather than a join predicate
        var residual = new AccessPredicate.Comparison(new AccessExpression.Arithmetic(ArithmeticOperator.Modulo,
                                                                                     new AccessExpression.Column(-1, "Id"),
                                                                                     Constant(40)),
                                                      ComparisonOperator.Equal,
                                                      Constant(0));

        var (_, emits) = await RunAsync(context, Definition(context, Between(500, 600), residual));

        // The heap holds 500 to 600 in twenties, so only those also dividing by forty survive the inner seek's residual
        Assert.Equal([520L, 560L, 600L], emits.Select(e => Value(e.InnerRecord, "Id")));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Residual_Reading_Only_The_Inner_Row_Is_Allowed()
    {
        var context = await LoadAsync();

        // CreatedDate belongs to ClusteredTable alone, so the inner row resolves it and the guard has nothing to object to
        var residual = new AccessPredicate.Not(new AccessPredicate.IsNull(new AccessExpression.Column(-1, "CreatedDate")));

        var (_, emits) = await RunAsync(context, Definition(context, Between(500, 600), residual));

        Assert.NotEmpty(emits);
    }

    private sealed record Context(DatabaseSource Database,
                                  NestedLoopsIterator Service,
                                  AllocationUnit Outer,
                                  AllocationUnit Inner);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        return new Context(database,
                           serviceHost.GetService<NestedLoopsIterator>(),
                           DemoDatabase.Unit(database, DemoDatabase.HeapTable, DemoDatabase.HeapIndex),
                           DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex));
    }

    private static NestedLoopsDefinition Definition(Context context,
                                                    SeekBounds outerBounds,
                                                    AccessPredicate? residual = null,
                                                    JoinType joinType = JoinType.Inner)
    {
        var outerInput = new RangeDefinition(context.Outer.AllocationUnitId, context.Outer.RootPage, [outerBounds]) { NodeId = 0 };

        var innerInput = new SeekDefinition(context.Inner.AllocationUnitId,
                                            context.Inner.RootPage,
                                            [new CorrelationBinding("Id", "Id")]) { NodeId = 1, Residual = residual };

        return new NestedLoopsDefinition(outerInput, innerInput)
        {
            NodeId = 2,
            JoinType = joinType
        };
    }

    private static async Task<(List<AccessStep> Steps, List<AccessStep.JoinEmit> Emits)> RunAsync(Context context,
                                                                                                  IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var emits = new List<AccessStep.JoinEmit>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.JoinEmit emit)
            {
                emits.Add(emit);
            }
        }

        return (steps, emits);
    }

    private static long Value(IRecord? record, string column)
        => new RecordRowValueSource(record!).GetValue(-1, column).Numeric;

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessExpression Constant(int value)
        => new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, value));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));
}
