using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Providers.Server;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Server;

public class BufferPoolInfoProviderTests
{
    private static BufferPoolInfoProvider GetProvider()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var provider = new BufferPoolInfoProvider(connectionString);

        return provider;
    }

    [Fact]
    public async Task Can_Get_Buffer_Pool_Entries()
    {
        var provider = GetProvider();

        var bufferPoolEntries = await provider.GetBufferPoolEntries("AdventureWorks2022");

        Assert.NotNull(bufferPoolEntries.Clean);
        Assert.NotNull(bufferPoolEntries.Dirty);
    }
}