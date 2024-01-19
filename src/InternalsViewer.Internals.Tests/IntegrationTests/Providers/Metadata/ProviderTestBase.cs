using InternalsViewer.Internals.Connections.Server;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Metadata.Internals;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Engine;
using InternalsViewer.Internals.Services.Loaders.Records;
using InternalsViewer.Internals.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

public class ProviderTestBase(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    public LogLevel LogLevel { get; set; } = LogLevel.Debug;

    protected async Task<InternalMetadata> GetMetadata()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");
        var database = new DatabaseSource(ServerConnectionFactory.Create(c => c.ConnectionString = connectionString))
        {
            Name = "AdventureWorks2022",
            BootPage = new BootPage { FirstAllocationUnitsPage = new PageAddress(1, 20) }
        };

        var pageService = ServiceHelper.CreatePageService(TestOutput);

        var loader = new DataRecordLoader(TestLogger.GetLogger<DataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(TestOutput), pageService, loader);

        var service = new MetadataLoader(TestLogger.GetLogger<MetadataLoader>(TestOutput), dataReader);

        var metadata = await service.Load(database);

        return metadata;
    }
}