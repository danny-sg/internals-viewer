using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;

namespace InternalsViewer.Internals.Services.Indexes;

public class IndexService(IPageService pageService, IRecordService recordService)
{
    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    public async Task<IndexNode> GetNodes(DatabaseSource database, PageAddress rootPage)
    {
        var result = await GetIndexNodes(database, rootPage, 0);

        return result;
    }

    private async Task<IndexNode> GetIndexNodes(DatabaseSource database,
                                                PageAddress rootPage,
                                                int level)
    {
        var node = new IndexNode(rootPage);

        node.Level = level;

        var page = await PageService.GetPage<IndexPage>(database, rootPage);

        var records = RecordService.GetIndexRecords(page);

        var downPagePointers = records.Select(r => r.DownPagePointer)
                                      .Where(p => p != PageAddress.Empty)
                                      .Distinct()
                                      .ToList();

        if (page.PageHeader.Level >= 1)
        {
            foreach (var childNode in downPagePointers)
            {
                node.Children.Add(await GetIndexNodes(database, childNode, level + 1));
            }
        }

        return node;
    }
}
