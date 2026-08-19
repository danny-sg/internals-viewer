using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Records.Blob;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using InternalsViewer.Internals.Services.Loaders.Records;

namespace InternalsViewer.Internals.Services.Records;

/// <summary>
/// Service responsible for retrieving LOB data by following blob structures across pages
/// </summary>
public sealed class LobDataService(IPageService pageService) : ILobDataService
{
    private IPageService PageService { get; } = pageService;

    public async Task<byte[]> GetData(DatabaseSource database,
                                      RowIdentifier rowIdentifier,
                                      CancellationToken cancellationToken)
    {
        var (page, record) = await GetLobRecord(database, rowIdentifier, cancellationToken);

        var result = new byte[GetTotalLength(record)];

        await WriteRecordData(database, page, record, result, 0, cancellationToken);

        return result;
    }

    public async Task<LobDataPrefix> GetDataPrefix(DatabaseSource database,
                                                   RowIdentifier rowIdentifier,
                                                   int maxLength,
                                                   CancellationToken cancellationToken)
    {
        var (page, record) = await GetLobRecord(database, rowIdentifier, cancellationToken);

        var totalLength = GetTotalLength(record);

        var result = new byte[Math.Min(totalLength, Math.Max(0, maxLength))];

        await WriteRecordData(database, page, record, result, 0, cancellationToken);

        return new LobDataPrefix(result, totalLength);
    }

    private static int GetTotalLength(LobRecord record)
    {
        return record.BlobType switch
        {
            BlobType.SmallRoot
                => record.Size,
            BlobType.Data 
                => record.Length - LobRecord.DataOffset,
            BlobType.LargeRoot or BlobType.Internal
                => record.BlobChildren.Count > 0 ? record.BlobChildren.Max(c => c.Offset) : 0,
            _ => throw new InvalidOperationException($"Unsupported blob type: {record.BlobType}")
        };
    }

    /// <summary>
    /// Copies a leaf chunk, taking only as much of it as the destination still has room for
    /// </summary>
    /// <remarks>
    /// The destination is short of the blob when only a prefix was asked for, so the chunk that straddles the end of
    /// it is truncated rather than overrunning.
    /// </remarks>
    private static int CopyLeafData(LobPage page, LobRecord record, byte[] destination, int position)
    {
        var (sourceOffset, length) = record.BlobType == BlobType.SmallRoot
            ? (record.Offset + LobRecord.SmallDataOffset, (int)record.Size)
            : (record.Offset + LobRecord.DataOffset, record.Length - LobRecord.DataOffset);

        length = Math.Min(length, destination.Length - position);

        if (length <= 0)
        {
            return position;
        }

        page.Data
            .AsSpan(sourceOffset, length)
            .CopyTo(destination.AsSpan(position, length));

        return position + length;
    }

    private async Task<int> WriteRecordData(DatabaseSource database,
                                            LobPage page,
                                            LobRecord record,
                                            byte[] destination,
                                            int position,
                                            CancellationToken cancellationToken)
    {
        switch (record.BlobType)
        {
            case BlobType.SmallRoot:
            case BlobType.Data:

                return CopyLeafData(page, record, destination, position);

            case BlobType.LargeRoot:
            case BlobType.Internal:

                foreach (var child in record.BlobChildren)
                {
                    if (position >= destination.Length)
                    {
                        return position;
                    }

                    if (child.RowIdentifier is null)
                    {
                        continue;
                    }

                    var (childPage, childRecord) = await GetLobRecord(database,
                                                                      child.RowIdentifier,
                                                                      cancellationToken);

                    position = await WriteRecordData(database,
                                                     childPage,
                                                     childRecord,
                                                     destination,
                                                     position,
                                                     cancellationToken);
                }

                return position;

            default:
                throw new InvalidOperationException($"Unsupported blob type: {record.BlobType}");
        }
    }

    private async Task<(LobPage Page, LobRecord Record)> GetLobRecord(DatabaseSource database,
                                                                      RowIdentifier rowIdentifier,
                                                                      CancellationToken cancellationToken)
    {
        var page = await PageService.GetPage<LobPage>(database,
                                                      rowIdentifier.PageAddress,
                                                      cancellationToken,
                                                      isMarkEnabled: false);

        if (rowIdentifier.SlotId >= page.OffsetTable.Length)
        {
            throw new InvalidOperationException(
                $"Slot {rowIdentifier.SlotId} not found on page {rowIdentifier.PageAddress}");
        }

        var record = LobRecordLoader.Load(page, page.OffsetTable[rowIdentifier.SlotId]);

        record.Slot = rowIdentifier.SlotId;

        return (page, record);
    }
}