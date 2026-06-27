using System.Buffers;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Indexes;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;

namespace InternalsViewer.Internals.Services.Indexes;

/// <summary>
/// Service to provide index structure information
/// </summary>
public sealed class IndexService(IPageService pageService, IRecordService recordService)
{
    /// <summary>
    /// Maximum number of pages read concurrently. Each read opens its own connection (server) or
    /// file handle (data file), so this keeps demand well under the SqlClient pool limit (default
    /// 100) and avoids a file-handle storm. Loading is otherwise sequential.
    /// </summary>
    private const int MaxParallelPageLoads = 16;

    private IPageService PageService { get; } = pageService;

    private IRecordService RecordService { get; } = recordService;

    /// <summary>
    /// Gets the index nodes for an index, starting from a root node page address
    /// </summary>
    public async Task<List<IndexNode>> GetNodes(DatabaseSource database, PageAddress rootPage)
    {
        var nodes = new List<IndexNode>();

        var nodesByAddress = new Dictionary<PageAddress, IndexNode>();

        var rootNode = new IndexNode(rootPage) { Level = 0, Ordinal = 1 };

        nodes.Add(rootNode);

        nodesByAddress[rootPage] = rootNode;

        var currentLevel = new List<IndexNode> { rootNode };
        byte level = 0;

        while (currentLevel.Count > 0)
        {
            // I/O for the whole level, in parallel.
            var loaded = await LoadLevel(database, currentLevel);

            // Node construction for the next level, single-threaded and in order.
            var nextLevel = new List<IndexNode>();

            for (var i = 0; i < currentLevel.Count; i++)
            {
                var node = currentLevel[i];
                var page = loaded[i];

                node.PageType = page.PageType;
                node.PreviousPage = page.PreviousPage;
                node.NextPage = page.NextPage;
                node.IndexLevel = page.IndexLevel;

                foreach (var childAddress in page.DownPointers)
                {
                    node.Children.Add(childAddress);

                    if (!nodesByAddress.TryGetValue(childAddress, out var childNode))
                    {
                        childNode = new IndexNode(childAddress)
                        {
                            Level = (byte)(level + 1),
                            Ordinal = (ushort)(nextLevel.Count + 1)
                        };

                        nodes.Add(childNode);
                        
                        nodesByAddress[childAddress] = childNode;

                        nextLevel.Add(childNode);
                    }

                    if (!childNode.Parents.Contains(node.PageAddress))
                    {
                        childNode.Parents.Add(node.PageAddress);
                    }
                }
            }

            currentLevel = nextLevel;
            level++;
        }

        return nodes;
    }

    /// <summary>
    /// Reads every page on a level in parallel, returning results in the same order as the input.
    /// </summary>
    private async Task<LoadedPage[]> LoadLevel(DatabaseSource database, List<IndexNode> levelNodes)
    {
        var results = new LoadedPage[levelNodes.Count];

        await Parallel.ForEachAsync(Enumerable.Range(0, levelNodes.Count),
                                    new ParallelOptions { MaxDegreeOfParallelism = MaxParallelPageLoads },
                                    async (i, _) 
                                        => results[i] = await LoadPage(database, levelNodes[i].PageAddress));

        return results;
    }

    /// <summary>
    /// Reads a single page and extracts the header fields and child pointers needed to build the tree
    /// </summary>
    private async Task<LoadedPage> LoadPage(DatabaseSource database, PageAddress pageAddress)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(PageData.Size);

        try
        {
            var page = await PageService.GetPage(database, pageAddress, buffer);

            var downPointers = new List<PageAddress>();

            // Only index pages above the leaf (Level >= 1) point down to child pages.
            if (page is IndexPage indexPage && page.PageHeader.Level >= 1)
            {
                downPointers = RecordService.GetIndexRecords(indexPage)
                                            .Select(r => r.DownPagePointer)
                                            .Where(p => p != PageAddress.Empty)
                                            .ToList();
            }

            return new LoadedPage(page.PageHeader.PageType,
                                  page.PageHeader.PreviousPage,
                                  page.PageHeader.NextPage,
                                  page.PageHeader.Level,
                                  downPointers);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private readonly record struct LoadedPage(PageType PageType,
                                              PageAddress PreviousPage,
                                              PageAddress NextPage,
                                              byte IndexLevel,
                                              List<PageAddress> DownPointers);
}
