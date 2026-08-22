using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services;

public sealed class DatabaseIdTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    [RequiresConnectionStringFact("local")]
    public async Task Database_Id_Matches_The_Server()
    {
        var database = await LoadDatabase();

        await using var connection = new SqlConnection(ConnectionStringHelper.GetConnectionString("local"));

        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT DB_ID()", connection);

        var expected = Convert.ToInt16(await command.ExecuteScalarAsync());

        TestOutput.WriteLine($"boot page reports {database.DatabaseId}, server reports {expected}");

        Assert.Equal(expected, database.DatabaseId);
    }
}
