using InternalsViewer.Internals.Services.Indexes;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.Helpers;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Indexes;

public class IndexServiceTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [Fact]
    public async Task Can_Get_Index_Nodes()
    {
        var serviceHost = new TestServiceHost();

        var service = serviceHost.GetService<IndexService>();

        var connection = new FileConnectionFactory().Create(c => c.Filename = "./IntegrationTests/Test Data/TestDatabase/TestDatabase.mdf");

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection);

        var result = await service.GetNodes(database, new PageAddress(1,1688));

    }
}
