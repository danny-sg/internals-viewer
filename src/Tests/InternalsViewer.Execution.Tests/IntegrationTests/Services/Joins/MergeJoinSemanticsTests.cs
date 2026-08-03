using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Joins;
using System.Data;
using InternalsViewer.Execution.AccessPaths.Binding;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Iterators.Joins;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Joins;

public class MergeJoinSemanticsTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    private const string MdfPath = "./IntegrationTests/Test Data/TestDatabase.mdf";

    [RequiresFileFact(MdfPath)]
    public async Task Inner_Join_Drops_Rows_With_No_Partner()
    {
        var emits = await RunAsync(JoinType.Inner, outer: (100, 110), inner: (105, 120));

        Assert.All(emits, e => Assert.False(e.IsUnmatched));
        Assert.Equal(Enumerable.Range(105, 6).Select(v => (long?)v), emits.Select(e => Value(e.OuterRecord)));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Outer_Join_Preserves_Unmatched_Outer_Rows()
    {
        var emits = await RunAsync(JoinType.LeftOuter, outer: (100, 110), inner: (105, 120));

        // 100 to 104 have no partner but are still returned, with the inner side left null
        var unmatched = emits.Where(e => e.IsUnmatched).ToList();

        Assert.Equal(Enumerable.Range(100, 5).Select(v => (long?)v), unmatched.Select(e => Value(e.OuterRecord)));
        Assert.All(unmatched, e => Assert.Null(e.InnerRecord));

        Assert.Equal(11, emits.Count);
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Outer_Join_Drains_The_Preserved_Side()
    {
        // The inner runs out first, so the rows left on the outer still have to be read out
        var emits = await RunAsync(JoinType.LeftOuter, outer: (100, 110), inner: (100, 104));

        Assert.Equal(11, emits.Count);
        Assert.Equal(Enumerable.Range(105, 6).Select(v => (long?)v),
                     emits.Where(e => e.IsUnmatched).Select(e => Value(e.OuterRecord)));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Right_Outer_Join_Preserves_Unmatched_Inner_Rows()
    {
        var emits = await RunAsync(JoinType.RightOuter, outer: (105, 120), inner: (100, 110));

        var unmatched = emits.Where(e => e.IsUnmatched).ToList();

        Assert.Equal(Enumerable.Range(100, 5).Select(v => (long?)v), unmatched.Select(e => Value(e.InnerRecord)));
        Assert.All(unmatched, e => Assert.Null(e.OuterRecord));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Full_Outer_Join_Preserves_Both_Sides()
    {
        var emits = await RunAsync(JoinType.FullOuter, outer: (100, 110), inner: (105, 120));

        Assert.Equal(5, emits.Count(e => e.IsUnmatched && e.OuterRecord is not null));
        Assert.Equal(10, emits.Count(e => e.IsUnmatched && e.InnerRecord is not null));
        Assert.Equal(6, emits.Count(e => !e.IsUnmatched));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Semi_Join_Emits_The_Outer_Row_Once()
    {
        var emits = await RunAsync(JoinType.LeftSemi, outer: (100, 110), inner: (105, 120));

        Assert.Equal(Enumerable.Range(105, 6).Select(v => (long?)v), emits.Select(e => Value(e.OuterRecord)));
        Assert.All(emits, e => Assert.Null(e.InnerRecord));
    }

    [RequiresFileFact(MdfPath)]
    public async Task Left_Anti_Semi_Join_Emits_Only_Unmatched_Outer_Rows()
    {
        var emits = await RunAsync(JoinType.LeftAntiSemi, outer: (100, 110), inner: (105, 120));

        Assert.Equal(Enumerable.Range(100, 5).Select(v => (long?)v), emits.Select(e => Value(e.OuterRecord)));
        Assert.All(emits, e => Assert.True(e.IsUnmatched));
    }

    private async Task<List<AccessStep.JoinEmit>> RunAsync(JoinType joinType, (int From, int To) outer, (int From, int To) inner)
    {
        var context = await LoadAsync();

        await context.Service.OpenAsync(new IteratorContext(context.Database),
                                        new MergeJoinDefinition(SideInput(context.Unit, Between(outer.From, outer.To), 0), SideInput(context.Unit, Between(inner.From, inner.To), 1))
        {
            JoinType = joinType
        },
                                        CancellationToken.None);

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

    private sealed record Context(DatabaseSource Database, MergeJoinStepIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var connection = new FileConnectionFactory().Create(c => c.Filename = MdfPath);

        var database = await serviceHost.GetService<IDatabaseService>()
                                        .LoadAsync("TestDatabase", connection, CancellationToken.None);

        var unit = DemoDatabase.Unit(database, DemoDatabase.ClusteredTable, DemoDatabase.ClusteredIndex);

        return new Context(database, serviceHost.GetService<MergeJoinStepIterator>(), unit);
    }

    private static JoinInputDefinition SideInput(AllocationUnit unit, SeekBounds bounds, int nodeId)
        => new(new RangeDefinition(unit.AllocationUnitId, unit.RootPage, [bounds]) { NodeId = nodeId }, ["Id"]);

    private static long? Value(IRecord? record)
        => record is null ? null : new RecordRowValueSource(record).GetValue(-1, "Id").Numeric;

    private static SeekBounds Between(int from, int to)
        => SeekBounds.Between(Key(from), Key(to));

    private static AccessKey Key(int value)
        => AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, value).WithColumnName("Id"));
}
