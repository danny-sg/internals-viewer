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

    public async Task<List<IndexNode>> GetNodes(DatabaseSource database, PageAddress rootPage)
    {
        var nodes = new List<IndexNode>();

        var rootNode = new IndexNode(rootPage) { Ordinal = 1 };

        nodes.Add(rootNode);

        await GetIndexNodes(nodes, database, rootPage, null, 0);

        return nodes;
    }

    private async Task GetIndexNodes(ICollection<IndexNode> nodes,
                                     DatabaseSource database,
                                     PageAddress pageAddress,
                                     PageAddress? parentPageAddress,
                                     int level)
    {

        var node = nodes.FirstOrDefault(n => n.PageAddress == pageAddress);

        if (node is null)
        {
            node = new IndexNode(pageAddress)
            {
                Level = level,
                Ordinal = nodes.Count(n => n.Level == level) + 1
            };
            nodes.Add(node);
        }

        if (parentPageAddress != null && !node.Parents.Contains(parentPageAddress.Value))
        {
            node.Parents.Add(parentPageAddress.Value);
        }

        var page = await PageService.GetPage(database, pageAddress);

        node.PageType = page.PageHeader.PageType;

        if (page is IndexPage indexPage)
        {
            var records = RecordService.GetIndexRecords(indexPage);

            var downPagePointers = records.Select(r => r.DownPagePointer)
                                          .Where(p => p != PageAddress.Empty)
                                          .ToList();

            if (page.PageHeader.Level >= 1)
            {
                foreach (var childNode in downPagePointers)
                {
                    await GetIndexNodes(nodes, database, childNode, pageAddress, level + 1);
                }
            }
        }
    }
}
