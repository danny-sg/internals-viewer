using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Services.Records.Loaders;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers.Internals;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;

namespace InternalsViewer.Internals.Readers.Internals;

public class TableReader(IPageService pageService): ITableReader
{
    public IPageService PageService { get; } = pageService;

    public async Task<List<DataRecord>> Read(DatabaseDetail database, PageAddress startPage, TableStructure structure)
    {
        var page = await PageService.GetPage<DataPage>(database, startPage);

        var records = new List<DataRecord>();

        while (true)
        {
            foreach (var offset in page.OffsetTable)
            {
                var record = DataRecordLoader.Load(page, offset, structure);

                records.Add(record);
            }

            if(page.PageHeader.NextPage == PageAddress.Empty)
            {
                break;
            }

            page = await PageService.GetPage<DataPage>(database, page.PageHeader.NextPage);
        }

        return records;
    }
}
