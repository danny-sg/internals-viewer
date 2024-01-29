using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Loaders.Compression;
using InternalsViewer.Internals.Services.Loaders.Records;
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
         //   new DataPageParser(new Services.Loaders.Compression.CompressionInfoLoader(new Services.Loaders.Records.CompressedDataRecordLoader(TestLogger.GetLogger<CompressedDataRecordLoader>(testOutput, logLevel))),
            new IndexPageParser(),
            new PfsPageParser(),
            new BootPageParser()
        };

        var service = new PageService(TestLogger.GetLogger<PageService>(testOutput, logLevel), loader, parsers);

        return service;
    }
}
