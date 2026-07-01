using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Providers.Server;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Server;

public class BufferPoolInfoProviderTests(ITestOutputHelper outputHelper)
{
    public ITestOutputHelper OutputHelper { get; } = outputHelper;

    private BufferPoolInfoProvider GetProvider()
    {
      
        var provider = new BufferPoolInfoProvider(new TestLogger<BufferPoolInfoProvider>(OutputHelper));

        return provider;
    }

    [Fact]
    public async Task Can_Get_Buffer_Pool_Entries()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var provider = GetProvider();

        var bufferPoolEntries = await provider.GetBufferPoolEntries(new DatabaseSource(new ServerConnectionType(new QueryPageReader(new TestLogger<QueryPageReader>(OutputHelper), connectionString), "Test", connectionString)));

        Assert.NotNull(bufferPoolEntries.Clean);
        Assert.NotNull(bufferPoolEntries.Dirty);
    }
}