using System.Data;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Tests.Helpers;

using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Results.Steps;
using InternalsViewer.Execution.Iterators.RowMode.Stepping;
using InternalsViewer.Execution.Iterators.RowMode.DataAccess;

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

        var (_, rows) = await RunAsync(context, Definition(context, 5000, null));

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
        var (_, rows) = await RunAsync(context, Definition(context, 5000, null));

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

        var (steps, rows) = await RunAsync(context, Definition(context, 5000, residual));

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

        var (_, rows) = await RunAsync(context, Definition(context, 5000, residual));

        Assert.Empty(rows);
    }

    private sealed record Context(DatabaseSource Database, IndexIterator Service, AllocationUnit Unit);

    private static async Task<Context> LoadAsync()
    {
        var serviceHost = new TestServiceHost();

        var database = await DemoDatabase.LoadAsync(serviceHost);

        return new Context(database,
                           serviceHost.GetService<IndexIterator>(),
                           DemoDatabase.Unit(database, DemoDatabase.CompressedTable, DemoDatabase.CompressedIndex));
    }

    private static RangeDefinition Definition(Context context, int id, AccessPredicate? residual)
    {
        var key = AccessKey.Create(AccessValue.FromInteger(SqlDbType.BigInt, id).WithColumnName("Id"));

        return new RangeDefinition(context.Unit.AllocationUnitId, context.Unit.RootPage, [SeekBounds.Equality(key)])
        {
            Residual = residual
        };
    }

    private async Task<(List<AccessStep> Steps, List<IRecordFields> Rows)> RunAsync(Context context, IteratorDefinition definition)
    {
        await using var stepper = new IteratorStepper(context.Service, definition, new IteratorContext(context.Database));

        var steps = new List<AccessStep>();

        var rows = new List<IRecordFields>();

        while (await stepper.StepNextAsync(CancellationToken.None) is { } step)
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
