using InternalsViewer.Internals.Services.Indexes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Connections.File;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Services.Records;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Readers.Internals;
using InternalsViewer.Internals.Services.Loaders.Engine;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Indexes;

public class IndexServiceTests(ITestOutputHelper testOutput)
{
    public ITestOutputHelper TestOutput { get; } = testOutput;

    [Fact]
    public async Task Can_Get_Index_Nodes()
    {
        var serviceHost = new TestServiceHost();

        var service = serviceHost.GetService<IndexService>();

        var connection = FileConnectionFactory.Create(c => c.Filename = "./IntegrationTests/Test Data/TestDatabase/TestDatabase.mdf");

        var databaseService = serviceHost.GetService<IDatabaseService>();

        var database = await databaseService.LoadAsync("TestDatabase", connection);

        var result = await service.GetNodes(database, new PageAddress(1,1688));

    }
}
