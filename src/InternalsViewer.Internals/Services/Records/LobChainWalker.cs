using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Blob;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Loaders.Records;

namespace InternalsViewer.Internals.Services.Records;

public static class LobChainWalker
{
    public static async Task WalkAsync(IPageService pageService,
                                       DatabaseSource database,
                                       RowIdentifier root,
                                       Action<PageAddress, int> onPage,
                                       CancellationToken cancellationToken)
    {
        var visited = new HashSet<PageAddress>();

        await WalkAsync(pageService, database, root, onPage, visited, cancellationToken);
    }

    private static async Task WalkAsync(IPageService pageService,
                                        DatabaseSource database,
                                        RowIdentifier identifier,
                                        Action<PageAddress, int> onPage,
                                        HashSet<PageAddress> visited,
                                        CancellationToken cancellationToken)
    {
        if (!visited.Add(identifier.PageAddress))
        {
            return;
        }

        var page = await pageService.GetPage<LobPage>(database, identifier.PageAddress, cancellationToken, isMarkEnabled: false);

        if (identifier.SlotId >= page.OffsetTable.Length)
        {
            return;
        }

        var record = LobRecordLoader.Load(page, page.OffsetTable[identifier.SlotId]);

        onPage(identifier.PageAddress, record.Data.Length);

        if (record.BlobType is BlobType.SmallRoot or BlobType.Data)
        {
            return;
        }

        foreach (var child in record.BlobChildren)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (child.RowIdentifier is not { } childIdentifier)
            {
                continue;
            }

            if (record.Level > 1)
            {
                await WalkAsync(pageService, database, childIdentifier, onPage, visited, cancellationToken);

                continue;
            }

            if (visited.Add(childIdentifier.PageAddress))
            {
                onPage(childIdentifier.PageAddress, child.Length);
            }
        }
    }
}
