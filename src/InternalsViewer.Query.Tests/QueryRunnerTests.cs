using System.Threading;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Tests.Helpers;
using InternalsViewer.Query.TransactionLog;
using Xunit.Abstractions;

namespace InternalsViewer.Query.Tests;

public class QueryRunnerTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Can_Run_Simple_Query()
    {
        var logger = TestLogger.GetLogger<QueryRunner>(TestOutputHelper);

        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        var query = "SELECT * FROM Person.Address";

        var eventReader = new EventReader(TestLogger.GetLogger<EventReader>(TestOutputHelper));

        var logReader = new LogRecordReader(TestLogger.GetLogger<LogRecordReader>(TestOutputHelper));
        var executor = new QueryRunner(logger, eventReader, logReader);

        var result = await executor.TraceQuery(query, connectionString, clearBufferPool: true, true, true,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.EngineEvents);
        Assert.NotEmpty(result.EngineEvents);

        foreach (var e in result.EngineEvents)
        {
            TestOutputHelper.WriteLine(e.ToString());
        }
    }

    [Fact]
    public async Task Invalid_Query_Gives_IsSuccess_False()
    {
        var logger = TestLogger.GetLogger<QueryRunner>(TestOutputHelper);

        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        var query = "SELECT * FROM Person.AddressZZZ";

        var eventReader = new EventReader(TestLogger.GetLogger<EventReader>(TestOutputHelper));

        var logReader = new LogRecordReader(TestLogger.GetLogger<LogRecordReader>(TestOutputHelper));
        var executor = new QueryRunner(logger, eventReader, logReader);

        var result = await executor.TraceQuery(query, connectionString, clearBufferPool: true, true, true,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
