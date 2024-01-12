using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Readers.Pages;
using InternalsViewer.Internals.Services.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Services.Pages.Parsers;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.Helpers;

internal class ServiceHelper
{
    internal static PageService CreatePageService(ITestOutputHelper testOutput, LogLevel logLevel = LogLevel.Debug)
    {
        var loader = new PageLoader();

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
}
