using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Replay.Tests.Helpers;
using Xunit.Abstractions;

namespace InternalsViewer.Replay.Tests;

public class QueryCaptureExecutorTests(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    [Fact]
    public async Task Can_Run_Simple_Query()
    {
        var logger = TestLogger.GetLogger<QueryCaptureExecutor>(TestOutputHelper);

        var connectionString = ConnectionStringHelper.GetConnectionString("Local");

        var query = "SELECT * FROM Person.Address";

        var executor = new QueryCaptureExecutor(logger);

        var result = await executor.TraceQuery(query, connectionString, clearBufferPool: true);

        Assert.NotNull(result);
        Assert.NotEmpty(result);

        foreach(var e in result)
        {
          TestOutputHelper.WriteLine(e.ToString());
        }
    }
}
