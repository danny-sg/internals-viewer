using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Records.Data;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Services.Records.Loaders;
using InternalsViewer.Internals.Metadata;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers.Internals;

namespace InternalsViewer.Internals.Readers.Internals;

public class TableReader(IPageLoader pageLoader): ITableReader
{
    public IPageLoader PageLoader { get; } = pageLoader;

    public async Task<List<DataRecord>> Read(DatabaseDetail databaseDetail, PageAddress startPage, TableStructure structure)
    {
        var page = await PageLoader.Load<Page>(databaseDetail, startPage);

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

            page = await PageLoader.Load<Page>(databaseDetail, page.PageHeader.NextPage);
        }

        return records;
    }
}
