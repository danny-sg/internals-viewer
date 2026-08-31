using System.Diagnostics;
using System.Threading;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Services.Records;

namespace InternalsViewer.Internals.Columnstore.Services;

public sealed class ColumnstorePageMapper(IPageService pageService,
                                          ColumnstoreCache cache,
                                          ILogger<ColumnstorePageMapper>? logger = null)
{
    public const long DefaultSizeLimit = 100L * 1024 * 1024;

    public async Task MapAsync(DatabaseSource database,
                               ColumnStoreIndex index,
                               CancellationToken cancellationToken,
                               long sizeLimit = DefaultSizeLimit)
    {
        var size = index.CompressedRowGroups.Sum(r => r.Segments.Sum(s => s.OnDiskSize));

        if (sizeLimit > 0 && size > sizeLimit)
        {
            logger?.LogInformation("Skipped columnstore page mapping for {Index}, {Size} bytes is over the {Limit} byte limit",
                                   index.IndexName ?? index.TableName,
                                   size,
                                   sizeLimit);

            return;
        }

        var start = Stopwatch.GetTimestamp();

        var pages = 0;

        foreach (var rowGroup in index.CompressedRowGroups)
        {
            foreach (var segment in rowGroup.Segments.Where(s => s.Column is { IsInternal: false } && !s.DataPointer.IsEmpty))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var read = new ColumnstorePageRead(PageAddress.Empty,
                                                   segment.Key.RowGroupId,
                                                   segment.Key.ColumnId,
                                                   segment.Column?.Name ?? string.Empty,
                                                   segment.Key.RowGroupId,
                                                   -1,
                                                   ColumnstoreReadType.Segment);

                pages += await MapChainAsync(database, segment.DataPointer, read, cancellationToken);
            }
        }

        foreach (var dictionary in Dictionaries(index))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var read = new ColumnstorePageRead(PageAddress.Empty,
                                               -1,
                                               dictionary.ColumnId,
                                               string.Empty,
                                               -1,
                                               dictionary.DictionaryId,
                                               ColumnstoreReadType.Dictionary);

            pages += await MapChainAsync(database, dictionary.DataPointer, read, cancellationToken);
        }

        logger?.LogInformation("Mapped {Pages} columnstore pages for {Index} in {Duration}",
                               pages,
                               index.IndexName ?? index.TableName,
                               Stopwatch.GetElapsedTime(start));
    }

    private async Task<int> MapChainAsync(DatabaseSource database,
                                          LobPointer pointer,
                                          ColumnstorePageRead read,
                                          CancellationToken cancellationToken)
    {
        if (pointer.IsEmpty)
        {
            return 0;
        }

        var count = 0;

        try
        {
            await LobChainWalker.WalkAsync(pageService,
                                           database,
                                           new RowIdentifier(pointer.PageAddress, (ushort)pointer.Slot),
                                           (address, bytes) =>
                                           {
                                               cache.SetPageRead(database,
                                                                 read with { PageAddress = address, Bytes = bytes });

                                               count++;
                                           },
                                           cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogDebug(exception, "Could not map the page chain at {Page}", pointer.PageAddress);
        }

        return count;
    }

    private static IEnumerable<SegmentDictionary> Dictionaries(ColumnStoreIndex index)
    {
        foreach (var column in index.Columns)
        {
            if (column.GlobalDictionary is { } global && !global.DataPointer.IsEmpty)
            {
                yield return global;
            }
        }

        foreach (var segment in index.CompressedRowGroups.SelectMany(r => r.Segments))
        {
            if (segment.LocalDictionary is { } local && !local.DataPointer.IsEmpty)
            {
                yield return local;
            }
        }
    }
}
