using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Providers.Server;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Server;

public class ServerInfoProviderTests
{
    private ServerInfoProvider GetProvider()
    {
        var databaseInfoProvider = new ServerInfoProvider();

        return databaseInfoProvider;
    }

    [Fact]
    public async Task Can_Get_Databases()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var databaseInfoProvider = GetProvider();

        var databases = await databaseInfoProvider.GetDatabases(connectionString);

        Assert.True(databases.Count > 1);

        var model = databases.FirstOrDefault(d => d.Name == "model");

        Assert.NotNull(model);

        Assert.Equal(3, model.DatabaseId);
        Assert.Equal("model", model.Name);
        Assert.Equal(DatabaseState.Online, model.State);
        Assert.Equal(160, model.CompatibilityLevel);
    }
}