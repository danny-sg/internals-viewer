using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Metadata.Structures;
using InternalsViewer.Internals.Services.Loaders.Records.FixedVar;

namespace InternalsViewer.Internals.Readers.Internals;

public sealed partial class RecordReader(ILogger<RecordReader> logger, 
                                 IPageService pageService, 
                                 FixedVarDataRecordLoader fixedVarDataRecordLoader)
    : IRecordReader
{
    private ILogger<RecordReader> Logger { get; } = logger;

    private IPageService PageService { get; } = pageService;

    private FixedVarDataRecordLoader FixedVarDataRecordLoader { get; } = fixedVarDataRecordLoader;

    public async Task<List<DataRecord>> Read(DatabaseSource database, 
                                             PageAddress startPage, 
                                             TableStructure structure,
                                             CancellationToken cancellationToken)
    {
        LogReadingRecords(Logger, startPage, structure);

        var page = await PageService.GetPage<DataPage>(database, startPage, cancellationToken, false);

        var records = new List<DataRecord>();

        while (true)
        {
            records.AddRange(page.OffsetTable.Select(offset =>
            {
                LogLoadingRecord(Logger, page.PageHeader.PageAddress.FileId, page.PageHeader.PageAddress.PageId, offset);

                return FixedVarDataRecordLoader.Load(page, offset, structure);
            }));

            var nextPage = page.PageHeader.NextPage;

            if (nextPage == PageAddress.Empty)
            {
                break;
            }

            LogNextPageNextPage(Logger, nextPage);

            page = await PageService.GetPage<DataPage>(database, nextPage, cancellationToken, false);
        }

        return records;
    }
}
