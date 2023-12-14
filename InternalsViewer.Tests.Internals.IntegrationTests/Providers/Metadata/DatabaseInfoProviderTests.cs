using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Tests.Internals.IntegrationTests.Helpers;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Providers.Metadata;

public class DatabaseInfoProviderTests
{
    private DatabaseInfoProvider GetProvider()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "master" };

        var databaseInfoProvider = new DatabaseInfoProvider(connection);

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

    [Fact]
    public async Task Can_Get_Allocation_Units()
    {
        var databaseInfoProvider = GetProvider();

        var allocationUnits = await databaseInfoProvider.GetAllocationUnits(); 
        
        Assert.True(allocationUnits.Count > 0);

        var first = allocationUnits.First();

        Assert.True(first.ObjectId > 0);
        Assert.NotEqual(new PageAddress(0,0), first.FirstIamPage);
        Assert.NotEmpty(first.SchemaName);
        Assert.NotEmpty(first.TableName);
        Assert.True(first.IsSystem);
        Assert.True(first.UsedPages > 0);
        Assert.True(first.TotalPages > 0);
    }

    [Fact]
    public async Task Can_Get_Compatibility_Level()
    {
        var databaseInfoProvider = GetProvider();

        var databaseId = await databaseInfoProvider.GetCompatibilityLevel("master");

        Assert.Equal(160, databaseId);
    }
}