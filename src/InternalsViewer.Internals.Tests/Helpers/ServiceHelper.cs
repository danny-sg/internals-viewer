using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Loaders.Compression;
using InternalsViewer.Internals.Services.Loaders.Records.Cd;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;
using InternalsViewer.Internals.Services.Pages;
using InternalsViewer.Internals.Services.Pages.Loaders;
using InternalsViewer.Internals.Services.Pages.Parsers;
using InternalsViewer.Internals.Services.Records;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Internals.Tests.Helpers;

internal static class ServiceHelper
{
    internal static PageService CreatePageService(ITestOutputHelper testOutput, LogLevel logLevel = LogLevel.Debug)
    {
        var loader = new PageLoader();

        var cdRecordLoader = new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(testOutput, logLevel));
        var compressionInfoLoader = new CompressionInfoLoader(cdRecordLoader);

        var parsers = new IPageParser[]
        {
            new DataPageParser(compressionInfoLoader),
            new IndexPageParser(),
            new AllocationPageParser(),
            new IamPageParser(),
            new LobPageParser(),
            new PfsPageParser(),
            new BootPageParser(),
            new FileHeaderPageParser(),
            new EmptyPageParser()
        };

        var service = new PageService(TestLogger.GetLogger<PageService>(testOutput, logLevel), loader, parsers);

        return service;
    }

    internal static RecordService CreateRecordService(ITestOutputHelper testOutput)
    {
        var service = new RecordService(new FixedVarIndexRecordLoader(TestLogger.GetLogger<FixedVarIndexRecordLoader>(testOutput)),
                                        new FixedVarDataRecordLoader(TestLogger.GetLogger<FixedVarDataRecordLoader>(testOutput)),
                                        new CdDataRecordLoader(TestLogger.GetLogger<CdDataRecordLoader>(testOutput)),
                                        new CdIndexRecordLoader(TestLogger.GetLogger<CdIndexRecordLoader>(testOutput)));

        return service;
    }
}