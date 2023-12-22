using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Providers.Metadata;

public class ServerInfoProviderTests
{
    private ServerInfoProvider GetProvider()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var databaseInfoProvider = new ServerInfoProvider(connection);

        return databaseInfoProvider;
    }

    [Fact]
    public async Task Can_Get_Databases()
    {
        var databaseInfoProvider = GetProvider();

        var databases = await databaseInfoProvider.GetDatabases();

        Assert.True(databases.Count > 1);

        var model = databases.FirstOrDefault(d => d.Name == "model");

        Assert.NotNull(model);

        Assert.Equal(3, model.DatabaseId);
        Assert.Equal("model", model.Name);
        Assert.Equal(DatabaseState.Online, model.State);
        Assert.Equal(160, model.CompatibilityLevel);
    }
}