using System.Buffers;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;

namespace InternalsViewer.Internals.Services.Indexes;

/// <summary>
/// Service to provide index structure information
/// </summary>
public sealed class IndexService(IPageService pageService, IRecordService recordService)
{
    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    /// <summary>
    /// Gets the index nodes for an index, starting from a root node page address
    /// </summary>
    public async Task<List<IndexNode>> GetNodes(DatabaseSource database, PageAddress rootPage)
    {
        var nodes = new List<IndexNode>();

        var rootNode = new IndexNode(rootPage) { Ordinal = 1 };

        nodes.Add(rootNode);

        var buffer = ArrayPool<byte>.Shared.Rent(PageData.Size);

        try
        {
            await GetIndexNodes(nodes, database, rootPage, null, 0, buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return nodes;
    }

    private async Task GetIndexNodes(ICollection<IndexNode> nodes,
                                     DatabaseSource database,
                                     PageAddress pageAddress,
                                     PageAddress? parentPageAddress,
                                     byte level,
                                     byte[] buffer)
    {
        var node = nodes.FirstOrDefault(n => n.PageAddress == pageAddress);

        if (node is null)
        {
            node = new IndexNode(pageAddress)
            {
                Level = level,
                Ordinal = (ushort)(nodes.Count(n => n.Level == level) + 1)
            };

            nodes.Add(node);
        }

        if (parentPageAddress != null && !node.Parents.Contains(parentPageAddress.Value))
        {
            node.Parents.Add(parentPageAddress.Value);
        }

        var page = await PageService.GetPage(database, pageAddress, buffer);

        node.PageType = page.PageHeader.PageType;
        node.PreviousPage = page.PageHeader.PreviousPage;
        node.NextPage = page.PageHeader.NextPage;
        node.IndexLevel = page.PageHeader.Level;

        if (page is IndexPage indexPage)
        {
            var records = RecordService.GetIndexRecords(indexPage);

            var downPagePointers = records.Select(r => r.DownPagePointer)
                                          .Where(p => p != PageAddress.Empty)
                                          .ToList();

            if (page.PageHeader.Level >= 1)
            {
                foreach (var childPageAddress in downPagePointers)
                {
                    node.Children.Add(childPageAddress);

                    await GetIndexNodes(nodes, database, childPageAddress, pageAddress, (byte)(level + 1), buffer);
                }
            }
        }
    }
}
