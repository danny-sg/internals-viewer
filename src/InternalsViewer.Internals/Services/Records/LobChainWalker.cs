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
        await WalkAsync(pageService,
                        database,
                        root,
                        onPage,
                        [],
                        [],
                        cancellationToken);
    }

    private static async Task WalkAsync(IPageService pageService,
                                        DatabaseSource database,
                                        RowIdentifier identifier,
                                        Action<PageAddress, int> onPage,
                                        HashSet<RowIdentifier> visited,
                                        HashSet<PageAddress> reported,
                                        CancellationToken cancellationToken)
    {
        if (!visited.Add(identifier))
        {
            return;
        }

        var page = await pageService.GetPage<LobPage>(database, identifier.PageAddress, cancellationToken, isMarkEnabled: false);

        if (identifier.SlotId >= page.OffsetTable.Length)
        {
            return;
        }

        var record = LobRecordLoader.Load(page, page.OffsetTable[identifier.SlotId]);

        if (reported.Add(identifier.PageAddress))
        {
            onPage(identifier.PageAddress, record.Data.Length);
        }

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
                await WalkAsync(pageService, database, childIdentifier, onPage, visited, reported, cancellationToken);

                continue;
            }

            if (reported.Add(childIdentifier.PageAddress))
            {
                onPage(childIdentifier.PageAddress, child.Length);
            }
        }
    }
}
