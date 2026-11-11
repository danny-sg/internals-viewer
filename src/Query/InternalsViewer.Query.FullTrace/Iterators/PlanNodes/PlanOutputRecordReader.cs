using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Engine;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Pages;
using InternalsViewer.Internals.Interfaces.Services.Records;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

public sealed class PlanOutputRecordReader(IPageService pageService,
                                           IRecordService recordService,
                                           DatabaseSource database,
                                           ILogger logger)
{
    private const int PageLimit = 64;

    private const string NullText = "NULL";

    private Dictionary<PageAddress, List<IRecord>> Pages { get; } = [];

    public async Task<string?> ReadAsync(PlanOutputRecord record)
    {
        if (record.Location.DatabaseId != database.DatabaseId)
        {
            return null;
        }

        try
        {
            var records = await GetRecords(record.Location.Page);

            var field = records.Find(r => r.Offset == record.Location.Offset)?
                               .Fields
                               .Find(f => f.ColumnStructure.LeafOffset == record.ColumnOffset);

            return field is { IsNull: true } ? NullText : field?.Value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read the record at {Offset} on page {Page}", record.Location.Offset, record.Location.Page);

            return null;
        }
    }

    private async Task<List<IRecord>> GetRecords(PageAddress pageAddress)
    {
        if (Pages.TryGetValue(pageAddress, out var records))
        {
            return records;
        }

        var page = await pageService.GetPage(database, pageAddress, CancellationToken.None, false);

        records = [.. recordService.GetRecords(page)];

        if (Pages.Count >= PageLimit)
        {
            Pages.Clear();
        }

        Pages[pageAddress] = records;

        return records;
    }
}
