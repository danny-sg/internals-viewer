using System.Collections.Generic;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Services.Records.Loaders;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Readers.Internals;

public class TableReader(IPageService pageService)
{
    public IPageService PageService { get; } = pageService;

    public async Task<List<DataRecord>> Read(DatabaseDetail databaseDetail, PageAddress startPage, TableStructure structure)
    {
        var page = await PageService.Load<Page>(databaseDetail, startPage);

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

            page = await PageService.Load<Page>(databaseDetail, page.PageHeader.NextPage);
        }

        return records;
    }
}
