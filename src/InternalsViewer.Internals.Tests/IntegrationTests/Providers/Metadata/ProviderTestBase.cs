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
        var pageService = ServiceHelper.CreatePageService(TestOutput, LogLevel);

        var loader = new DataRecordLoader(TestLogger.GetLogger<DataRecordLoader>(TestOutput));

        var dataReader = new RecordReader(TestLogger.GetLogger<RecordReader>(testOutput, LogLevel), pageService, loader);

        var database = new DatabaseSource
        {
            Name = "TestDatabase",
            BootPage = new BootPage { FirstAllocationUnitsPage = new PageAddress(1, 20) }
        };

        var service = new MetadataLoader(TestLogger.GetLogger<MetadataLoader>(TestOutput, LogLevel), dataReader);

        var metadata = await service.Load(database);

        return metadata;
    }
}