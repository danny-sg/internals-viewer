using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Internals.Tests.Helpers;

internal class ServiceHelper
{
    internal static PageService CreateFilePageService(ITestOutputHelper testOutput)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var reader = new FilePageReader("");

        var loader = new PageLoader(reader);

        var parsers = new IPageParser[]
        {
            new DataPageParser(),
            new IndexPageParser(),
        };

        var service = new PageService(TestLogger.GetLogger<PageService>(testOutput), loader, parsers);

        return service;
    }

    internal static PageService CreateDatabasePageService(ITestOutputHelper testOutput)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var reader = new DatabasePageReader(new CurrentConnection { ConnectionString = connectionString });

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
