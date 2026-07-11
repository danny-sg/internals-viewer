using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Parsing;
using InternalsViewer.Query.Tests.Helpers;
using InternalsViewer.Query.TransactionLog;
using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Query.Events.EventTypes;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests.IntegrationTests;

public class ReadGroupingTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Are_Read_Events_Grouped_For_Heap()
    {
        var query = "SELECT TOP 10 * FROM dbo.HeapTable";

        await TestQuery(query);
    }

    [Fact]
    public async Task Are_Read_Events_Grouped_For_BTree()
    {
        var query = "SELECT TOP 10 * FROM dbo.ClusteredTable";

        await TestQuery(query);
    }

    private async Task TestQuery(string sql)
    {
        var results = await RunQuery(sql);

        foreach (var e in results.EngineEvents)
        {
            var detail = e is FileEvent f ? $" [Mode={f.Mode} Size={f.Size} {f.FromPageAddress}-{f.ToPageAddress}]" : "";

            TestOutputHelper.WriteLine($"{e.TimeUs}:{e.DurationUs} {e.Name} - {e.Description}{detail}  OBJ=[{e.ObjectName}]");

            if (e is NonCachedReadEventGroup grouped)
            {
                foreach (var g in grouped.Events)
                {
                    TestOutputHelper.WriteLine($"    {g.TimeUs}:{g.DurationUs} {g.Name} - {g.Description}");
                }
            }
        }
    }

    private async Task<QueryResult> RunQuery(string sql)
    {
        var logger = TestLogger.GetLogger<QueryRunner>(TestOutputHelper, LogLevel.Information);

        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        var eventReader = new EventReader(TestLogger.GetLogger<EventReader>(TestOutputHelper, LogLevel.Information));

        var logReader = new LogRecordReader(TestLogger.GetLogger<LogRecordReader>(TestOutputHelper, LogLevel.Information));
        var executor = new QueryRunner(logger, eventReader, logReader);

        var database = new DatabaseSource(
            new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(TestOutputHelper))
                .Create(c => c.ConnectionString = connectionString))
        {
            Name = "TestDatabase"
        };

        var payload = new ExecuteSqlPayload(sql, new QueryOptions(), StatementType.Select, null);

        var result = await executor.TraceQuery(payload, database, new EventOptions(), @"C:\Symbols", null,
            CancellationToken.None);

        return result;
    }
}
