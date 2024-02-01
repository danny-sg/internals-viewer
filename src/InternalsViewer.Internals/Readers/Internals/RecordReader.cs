using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Loaders.Records;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.Readers.Internals;

public class RecordReader(ILogger<RecordReader> logger, IPageService pageService, FixedVarRecord fixedVarRecord) : IRecordReader
{
    public ILogger<RecordReader> Logger { get; } = logger;

    public IPageService PageService { get; } = pageService;

    public FixedVarRecord FixedVarRecord { get; } = fixedVarRecord;

    public async Task<List<DataRecord>> Read(DatabaseSource database, PageAddress startPage, TableStructure structure)
    {
        Logger.LogTrace("Reading records from {StartPage} - {@Structure}", startPage, structure);

        var page = await PageService.GetPage<DataPage>(database, startPage);

        var records = new List<DataRecord>();

        while (true)
        {
            records.AddRange(page.OffsetTable.Select(offset =>
            {
                Logger.LogTrace("Loading record {FileId}:{PageId}:{Offset}", 
                                page.PageHeader.PageAddress.FileId, 
                                page.PageHeader.PageAddress.PageId, 
                                offset);

                return FixedVarRecord.Load(page, offset, structure);
            }));
            
            var nextPage = page.PageHeader.NextPage;

            if (nextPage == PageAddress.Empty)
            {
                Logger.LogTrace("Next page: None. Read complete");

                break;
            }

            Logger.LogTrace("Next page: {NextPage}", nextPage);

            page = await PageService.GetPage<DataPage>(database, nextPage);
        }

        return records;
    }
}
