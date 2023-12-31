using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Services.Pages.Parsers;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.Helpers;

internal class ServiceHelper
{
    internal static PageService CreateFilePageService(ITestOutputHelper testOutput, LogLevel logLevel = LogLevel.Debug)
    {
        var reader = new FilePageReader("");

        var loader = new PageLoader(reader);

        var parsers = new IPageParser[]
        {
            new DataPageParser(),
            new IndexPageParser(),
        };

        var service = new PageService(TestLogger.GetLogger<PageService>(testOutput, logLevel), loader, parsers);

        return service;
    }

    internal static PageService CreateDataFilePageService(ITestOutputHelper testOutput, LogLevel logLevel = LogLevel.Debug)
    {
        var reader = new DataFilePageReader("./IntegrationTests/Test Data/TestDatabase");

        var loader = new PageLoader(reader);

        var parsers = new IPageParser[]
        {
            new DataPageParser(),
            new IndexPageParser(),
            new PfsPageParser(),
            new BootPageParser()
        };

        var service = new PageService(TestLogger.GetLogger<PageService>(testOutput, logLevel), loader, parsers);

        return service;
    }


    internal static PageService CreateDatabasePageService(ITestOutputHelper testOutput, LogLevel logLevel = LogLevel.Debug)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var reader = new QueryPageReader(new CurrentConnection { ConnectionString = connectionString });

        var loader = new PageLoader(reader);

        var parsers = new IPageParser[]
        {
            new DataPageParser(),
            new IndexPageParser(),
        };

        var service = new PageService(TestLogger.GetLogger<PageService>(testOutput, logLevel), loader, parsers);

        return service;
    }
}
