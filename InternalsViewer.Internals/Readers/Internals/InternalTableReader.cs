using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Metadata.Internals.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Services.Records.Loaders;
using InternalsViewer.Internals.Metadata;

namespace InternalsViewer.Internals.Readers.Internals;

public class TableReader(IPageService pageService)
{
    public IPageService PageService { get; } = pageService;

    public async Task Read(Database database, PageAddress startPage, TableStructure structure)
    {
        var page = await PageService.Load<Page>(database, startPage);

        while (true)
        {
            foreach (var offset in page.OffsetTable)
            {
                var record = DataRecordLoader.Load(page, offset, structure);
            }

            if(page.PageHeader.NextPage == PageAddress.Empty)
            {
                break;
            }

            page = await PageService.Load<Page>(database, page.PageHeader.NextPage);
        }
    }
}
