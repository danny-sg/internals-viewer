using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Services.Pages.Parsers;
using InternalsViewer.Internals.Services.Pages;
using Xunit.Abstractions;

namespace InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;

internal class ServiceHelper
{
    internal static PageService CreatePageService(ITestOutputHelper testOutput)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = "AdventureWorks2022" };

        var reader = new DatabasePageReader(connection);

        var loader = new PageLoader(reader);

        var parsers = new IPageParser[]
        {
            new DataPageParser(),
            new IndexPageParser(),
        };

        var service = new PageService(TestLogger.GetLogger<PageService>(testOutput), loader, parsers);

        return service;
    }
}
