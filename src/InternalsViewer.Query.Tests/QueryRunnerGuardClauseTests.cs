using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.TransactionLog;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests;

/// <summary>
/// Covers the TraceQuery guard clauses that return before any database connection is opened,
/// so these run without a live SQL Server (unlike <see cref="QueryRunnerTests"/>).
/// </summary>
public class QueryRunnerGuardClauseTests(ITestOutputHelper testOutputHelper)
{
    private QueryRunner CreateRunner()
    {
        var logger = TestLogger.GetLogger<QueryRunner>(testOutputHelper);
        var eventReader = new EventReader(TestLogger.GetLogger<EventReader>(testOutputHelper));
        var logRecordReader = new LogRecordReader(TestLogger.GetLogger<LogRecordReader>(testOutputHelper));

        return new QueryRunner(logger, eventReader, logRecordReader);
    }

    private static DatabaseSource CreateFakeDatabase(ITestOutputHelper testOutputHelper)
    {
        const string fakeConnectionString =
            "Server=fake-host;Database=fake-db;Integrated Security=True;TrustServerCertificate=True;";

        return new DatabaseSource(
            new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(testOutputHelper))
                .Create(c => c.ConnectionString = fakeConnectionString))
        {
            Name = "FakeDatabase"
        };
    }

    [Fact]
    public async Task Empty_SqlText_Returns_Failure_Without_Touching_Database()
    {
        var runner = CreateRunner();
        var database = CreateFakeDatabase(testOutputHelper);

        var payload = new ExecuteSqlPayload("", new QueryOptions(), StatementType.Select, null);

        var result = await runner.TraceQuery(payload, database, new EventOptions(), @"C:\Symbols", null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Empty query text", result.Message);
        Assert.Equal(0, result.RowCount);
        Assert.Equal("None", result.SessionId);
    }

    [Fact]
    public async Task Multiple_GO_Separated_Batches_Are_Rejected_As_Untraceable()
    {
        var runner = CreateRunner();
        var database = CreateFakeDatabase(testOutputHelper);

        var payload = new ExecuteSqlPayload("SELECT 1\nGO\nSELECT 2", new QueryOptions(), StatementType.Select, null);

        var result = await runner.TraceQuery(payload, database, new EventOptions(), @"C:\Symbols", null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Multi-statement queries cannot be traced", result.Message);
    }

    [Fact]
    public async Task MultiStatementSelect_Without_TrackedSelection_Is_Rejected_As_Untraceable()
    {
        var runner = CreateRunner();
        var database = CreateFakeDatabase(testOutputHelper);

        var payload = new ExecuteSqlPayload("SELECT 1; SELECT 2;",
                                            new QueryOptions(),
                                            StatementType.MultiStatementSelect,
                                            null);

        var result = await runner.TraceQuery(payload, database, new EventOptions(), @"C:\Symbols", null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Multi-statement queries cannot be traced", result.Message);
    }

    [Fact]
    public async Task MultiStatementModification_Without_TrackedSelection_Is_Rejected_As_Untraceable()
    {
        var runner = CreateRunner();
        var database = CreateFakeDatabase(testOutputHelper);

        var payload = new ExecuteSqlPayload("UPDATE dbo.T SET X = 1; UPDATE dbo.T SET Y = 2;",
                                            new QueryOptions(),
                                            StatementType.MultiStatementModification,
                                            null);

        var result = await runner.TraceQuery(payload, database, new EventOptions(), @"C:\Symbols", null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Multi-statement queries cannot be traced", result.Message);
    }
}
