using System.Data;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Execution.Services.Indexes;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Services.Indexes;

/// <summary>
/// Access paths over a page compressed table, where a column taking no bytes at all means zero rather than null
/// </summary>
public class CompressedRecordTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task Seek_Finds_A_Row_In_A_Compressed_Clustered_Index()
    {
        var context = await LoadAsync();

        await StartAsync(context, 5000, null);

        var (_, rows) = await RunAsync(context.Service);

        var row = Assert.Single(rows);

        Assert.Equal("5000", Field(row, "Id"));

        // Compression stores this value in two bytes rather than the eight a bigint would take
        Assert.Equal(2, row.Fields.Single(f => f.Name == "Id").Data.Length);
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Column_Of_No_Length_Reads_As_Zero_Not_As_Blank()
    {
        var context = await LoadAsync();

        // Row 5000 divides exactly by 100, 10 and 1000, so those columns are all zero
        await StartAsync(context, 5000, null);

        var (_, rows) = await RunAsync(context.Service);

        var row = Assert.Single(rows);

        foreach (var name in new[] { "NumberField1", "NumberField2", "NumberField3", "DecimalField1" })
        {
            var field = row.Fields.Single(f => f.Name == name);

            Assert.False(field.IsNull);
            Assert.Empty(field.Data.ToArray());
            Assert.Equal("0", field.Value);
        }
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Predicate_Matches_A_Compressed_Zero()
    {
        var context = await LoadAsync();

        var residual = new AccessPredicate.Comparison(new AccessExpression.Column(-1, "NumberField1"),
                                                      ComparisonOperator.Equal,
                                                      new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, 0)));

        await StartAsync(context, 5000, residual);

        var (steps, rows) = await RunAsync(context.Service);

        // A column of no length is the value zero, so this has to match rather than evaluate as unknown against a null
        Assert.Single(rows);
        Assert.All(steps.OfType<AccessStep.Row>(), r => Assert.NotEqual(RowOutcome.Unknown, r.Outcome));
    }

    [RequiresFileFact(DemoDatabase.MdfPath)]
    public async Task A_Predicate_Rejects_A_Row_Whose_Compressed_Value_Differs()
    {
        var context = await LoadAsync();

        var residual = new AccessPredicate.Comparison(new AccessExpression.Column(-1, "NumberField1"),
                                                      ComparisonOperator.Equal,
                                                      new AccessExpression.Constant(AccessValue.FromInteger(SqlDbType.Int, 1)));

        await StartAsync(context, 5000, residual);

        var (_, rows) = await RunAsync(context.Service);

        Assert.Empty(rows);
    }

    private sealed record Context(DatabaseSource Database, IndexStepService Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        return new Context(database,
                           serviceHost.GetService<IndexStepService>(),
                           DemoDatabase.Unit(database, DemoDatabase.CompressedTable, DemoDatabase.CompressedIndex));
    }

    private static async Task StartAsync(Context context, int id, AccessPredicate? residual)
    {
        var key = AccessKey.Create(AccessValue.FromInteger(SqlDbType.BigInt, id).WithColumnName("Id"));

        await context.Service.StartAsync(context.Database,
                                         context.Unit.AllocationUnitId,
                                         context.Unit.RootPage,
                                         [SeekBounds.Equality(key)],
                                         residual,
                                         ScanDirection.Forward,
                                         CancellationToken.None);
    }

    private async Task<(List<AccessStep> Steps, List<IRecordFields> Rows)> RunAsync(IndexStepService service)
    {
        var steps = new List<AccessStep>();

        var rows = new List<IRecordFields>();

        while (await service.StepNextAsync(CancellationToken.None) is { } step)
        {
            steps.Add(step);

            if (step is AccessStep.Row { EmittedRecord: { } record })
            {
                rows.Add(new IRecordFields(record.Fields));

                TestOutput.WriteLine(string.Join(", ", record.Fields.Select(f => $"{f.Name}={f.Value}")));
            }
        }

        return (steps, rows);
    }

    private static string Field(IRecordFields row, string name)
        => row.Fields.Single(f => f.Name == name).Value;

    private sealed record IRecordFields(List<Internals.Engine.Records.RecordField> Fields);
}
