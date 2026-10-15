using System.Collections.Concurrent;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed class RowBufferPageResolver(DatabaseSource database,
                                          IPageService pageService,
                                          IRecordService recordService,
                                          ILogger logger)
{
    private ConcurrentDictionary<PageAddress, Task<PageSlots?>> Pages { get; } = new();

    public async Task<int?> GetSlotAsync(RowBufferPage pointer, CancellationToken cancellationToken)
    {
        if (await Pages.GetOrAdd(pointer.Page, LoadAsync).WaitAsync(cancellationToken) is not { } page || page.Lsn != pointer.Lsn)
        {
            return null;
        }

        var index = Array.FindLastIndex(page.Offsets, o => o <= pointer.Offset);

        return index < 0 ? null : page.Slots[index];
    }

    private async Task<PageSlots?> LoadAsync(PageAddress address)
    {
        try
        {
            var page = await pageService.GetPage(database, address, CancellationToken.None, isMarkEnabled: false);

            var records = recordService.GetRecords(page).OrderBy(r => r.Offset).ToList();

            return new PageSlots(page.PageHeader.Lsn, [.. records.Select(r => (int)r.Offset)], [.. records.Select(r => r.Slot)]);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Reading page {Page} for a row buffer failed", address);

            return null;
        }
    }

    private sealed record PageSlots(LogSequenceNumber Lsn, int[] Offsets, int[] Slots);
}
