using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.Services.Joins.Definitions;
using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Services.Joins;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

/// <summary>
/// The inner side is filtered to even keys, so every odd outer row drives a rebind that returns nothing
/// </summary>
public class NestedLoopsSemanticsTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    private static readonly long[] Even = [100, 102, 104, 106, 108];

    private static readonly long[] Odd = [101, 103, 105, 107, 109];

    [RequiresFileFact(MdfPath)]
    public async Task Inner_Join_Drops_Outer_Rows_Whose_Rebind_Returns_Nothing()
    {
        var emits = await RunAsync(JoinType.Inner);

        Assert.Equal(Even.Select(v => (long?)v), emits.Select(e => Value(e.OuterRecord)));
        Assert.All(emits, e => Assert.NotNull(e.InnerRecord));
        Assert.All(emits, e => Assert.False(e.IsUnmatched));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Outer_Join_Preserves_Outer_Rows_Whose_Rebind_Returns_Nothing()
    {
        var emits = await RunAsync(JoinType.LeftOuter);

        Assert.Equal(10, emits.Count);

        var unmatched = emits.Where(e => e.IsUnmatched).ToList();

        Assert.Equal(Odd.Select(v => (long?)v), unmatched.Select(e => Value(e.OuterRecord)));
        Assert.All(unmatched, e => Assert.Null(e.InnerRecord));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Semi_Join_Emits_The_Outer_Row_Alone()
    {
        var emits = await RunAsync(JoinType.LeftSemi);

        Assert.Equal(Even.Select(v => (long?)v), emits.Select(e => Value(e.OuterRecord)));
        Assert.All(emits, e => Assert.Null(e.InnerRecord));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Anti_Semi_Join_Emits_Only_The_Outer_Rows_With_No_Inner_Row()
    {
        var emits = await RunAsync(JoinType.LeftAntiSemi);

        Assert.Equal(Odd.Select(v => (long?)v), emits.Select(e => Value(e.OuterRecord)));
        Assert.All(emits, e => Assert.True(e.IsUnmatched));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Every_Outer_Row_Rebinds_Whatever_The_Join_Type()
    {
        var context = await LoadAsync();

        await StartAsync(context, JoinType.LeftAntiSemi);

        while (await context.Service.StepNextAsync(CancellationToken.None) is not null)
        {
        }

        Assert.Equal(10, context.Service.RebindCount);
    }

    private async Task<List<AccessStep.JoinEmit>> RunAsync(JoinType joinType)
    {
        var context = await LoadAsync();

        await StartAsync(context, joinType);

        var emits = new List<AccessStep.JoinEmit>();

        while (await context.Service.StepNextAsync(CancellationToken.None) is { } step)
        {
            if (step is AccessStep.JoinEmit emit)
            {
                emits.Add(emit);
            }
        }

        TestOutput.WriteLine($"{joinType}: {emits.Count} emitted");

        foreach (var emit in emits)
        {
            TestOutput.WriteLine($"  outer={Value(emit.OuterRecord)?.ToString() ?? "NULL"} "
                                 + $"inner={Value(emit.InnerRecord)?.ToString() ?? "NULL"} unmatched={emit.IsUnmatched}");
        }

        return emits;
    }

    private static async Task StartAsync(Context context, JoinType joinType)
    {
        var outerInput = new ScanDefinition(context.Unit.AllocationUnitId, context.Unit.RootPage, [Between(100, 109)]);

        var innerInput = new SeekDefinition(context.Unit.AllocationUnitId,
                                                   context.Unit.RootPage,
                                                   [new CorrelationBinding("Id", "Id")])
        {
            Residual = EvenKeysOnly()
        };

        await context.Service.StartAsync(context.Database,
                                         outerInput,
                                         innerInput,
                                         CancellationToken.None,
                                         joinType: joinType);
    }

    private static AccessPredicate EvenKeysOnly()
        => new AccessPredicate.Comparison(
            new AccessExpression.Arithmetic(ArithmeticOperator.Modulo,
                                            new AccessExpression.Column(-1, "Id"),
                                            new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, 2))),
            ComparisonOperator.Equal,
            new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, 0)));

    private sealed record Context(DatabaseSource Database, NestedLoopsStepService Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var database = await serviceHost.GetService<IDatabaseService>()
                                        .LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<NestedLoopsStepService>(), unit);
    }

    private static long? Value(IRecord? record)
        => record is null ? null : new RecordRowValueSource(record).GetValue(-1, "Id").Numeric;

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));
}
