using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed class RowBufferPageResolverFactory(IPageService pageService,
                                                 IRecordService recordService,
                                                 ILogger<RowBufferPageResolver> logger)
{
    public RowBufferPageResolver Create(DatabaseSource database) => new(database, pageService, recordService, logger);
}
