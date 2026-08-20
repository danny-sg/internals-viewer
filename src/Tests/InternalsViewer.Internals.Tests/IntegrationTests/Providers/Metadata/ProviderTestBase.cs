using InternalsViewer.Internals;
using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Tests.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class ProviderTestBase(ITestOutputHelper testOutput)
{
    protected ITestOutputHelper TestOutput { get; } = testOutput;

    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    protected DatabaseSource GetDatabase()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var database = new DatabaseSource(new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(TestOutput)).Create(c => c.ConnectionString = connectionString))
        {
            Name = "AdventureWorks2025",
            BootPage = new BootPage { FirstAllocationUnitsPage = new PageAddress(1, 20) }
        };

        return database;
    }

    protected async Task<DatabaseSource> LoadDatabase()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        using var host = Host.CreateDefaultBuilder()
                             .ConfigureServices((_, services) => services.RegisterServices())
                             .Build();

        var databaseService = host.Services.GetRequiredService<IDatabaseService>();

        var connection = new ServerConnectionFactory(TestLogger.GetLogger<QueryPageReader>(TestOutput))
            .Create(c => c.ConnectionString = connectionString);

        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        return await databaseService.LoadAsync(databaseName, connection, CancellationToken.None);
    }

    protected async Task<InternalMetadata> GetMetadata()
    {
        var database = GetDatabase();

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput),
                                          pageService,
                                          loader,
                                          new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(TestOutput)));

        var service = new MetadataLoader(TestLogger.GetLogger<MetadataLoader>(TestOutput), dataReader);

        var metadata = await service.Load(database, CancellationToken.None);

        return metadata;
    }
}