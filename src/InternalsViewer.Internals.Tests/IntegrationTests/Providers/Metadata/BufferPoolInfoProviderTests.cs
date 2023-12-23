using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class BufferPoolInfoProviderTests
{
    private static BufferPoolInfoProvider GetProvider()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var provider = new BufferPoolInfoProvider(connection);

        return provider;
    }

    [Fact]
    public async Task Can_Get_Buffer_Pool_Entries()
    {
        var provider = GetProvider();

        var bufferPoolEntries = await provider.GetBufferPoolEntries("AdventureWorks2022");

        Assert.True(bufferPoolEntries.Clean.Count > 0);
        Assert.True(bufferPoolEntries.Dirty.Count > 0);
    }
}