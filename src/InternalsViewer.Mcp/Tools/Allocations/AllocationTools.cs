using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Allocation.Enums;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Models.Context.Allocations;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using static InternalsViewer.Mcp.Tools.Allocations.AllocationUnitFinder;

namespace InternalsViewer.Mcp.Tools.Allocations;

[McpServerToolType]
public sealed class AllocationTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Tab id from list_tabs; the active tab with a database when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "allocations_files", ReadOnly = true)]
    [Description("The files of the database: file id, name, file group, type, size and used pages")]
    public async Task<string> Files([Description(TabDescription)] string? tab = null,
                                    CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(tab, cancellationToken);

        var table = new TextTable("File", "Name", "FileGroup", "Type", "Pages", "Used", "Size MB", "Path");

        foreach (var file in context.Files.OrderBy(f => f.FileId))
        {
            table.Add(file.FileId,
                      file.Name,
                      file.FileGroupId,
                      file.FileType,
                      Format.Number(file.TotalPages),
                      Format.Number(file.UsedPages),
                      file.TotalMb.ToString("N1"),
                      file.PhysicalName);
        }

        var builder = new StringBuilder();

        builder.AppendLine($"Database {context.Database?.Name}: {context.Files.Count} files");

        table.Write(builder);

        return builder.ToString();
    }

    [McpServerTool(Name = "allocations_objects", ReadOnly = true)]
    [Description("The allocation units of the database: the tables and indexes that own pages, with the allocation unit id, " +
                 "index type, partition, pages and the first IAM, root and first pages")]
    public async Task<string> Objects(
        [Description("Regex matched against the schema, table and index name")] string? name = null,
        [Description("Include system objects")] bool includeSystem = false,
        [Description("Index type: Heap, Clustered, NonClustered, ClusteredColumnStore, NonClusteredColumnStore etc.")]
        string? type = null,
        [Description("Most rows to list")] int limit = 50,
        [Description("Rows to skip")] int offset = 0,
        [Description(TabDescription)] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(tab, cancellationToken);

        var pattern = name is null ? null : new Regex(name, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        IndexType? indexType = null;

        if (type is not null)
        {
            indexType = Enum.TryParse<IndexType>(type, true, out var parsed)
                ? parsed
                : throw new McpException($"'{type}' is not an index type. Use {string.Join(", ", Enum.GetNames<IndexType>())}.");
        }

        var units = context.AllocationUnits
                            .Values
                            .Where(u => (includeSystem || !u.IsSystem)
                                        && (indexType is null || u.IndexType == indexType)
                                        && (pattern is null || pattern.IsMatch(Describe(u))))
                            .OrderBy(u => u.SchemaName, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(u => u.TableName, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(u => u.IndexId)
                            .ThenBy(u => u.PartitionNumber)
                            .ThenBy(u => u.AllocationUnitType)
                            .ToList();

        var table = new TextTable("AuId", "Object", "Index Type", "Unit", "Partition", "Pages", "Used", "First IAM", "Root", "First");

        foreach (var unit in units.Skip(offset).Take(limit))
        {
            table.Add(unit.AllocationUnitId,
                      Describe(unit),
                      unit.IndexType,
                      unit.AllocationUnitType,
                      unit.PartitionNumber,
                      Format.Number(unit.TotalPages),
                      Format.Number(unit.UsedPages),
                      Location(unit.FirstIamPage),
                      Location(unit.RootPage),
                      Location(unit.FirstPage));
        }

        var builder = new StringBuilder();

        builder.AppendLine($"{units.Count} allocation units in {context.Database?.Name}");

        table.Write(builder);

        builder.AppendIfNotEmpty(Format.More(units.Count, offset + limit, "use offset and limit"));

        return builder.ToString();
    }

    [McpServerTool(Name = "allocations_page", ReadOnly = true)]
    [Description("Who owns a page and what the allocation maps say about it: the allocation unit, the PFS byte (allocated, free " +
                 "space, IAM, mixed extent, ghost records) and the GAM, SGAM, DCM and BCM bits of its extent")]
    public async Task<string> Page(
        [Description("Page address as file:page, for example 1:1048")] string page,
        [Description(TabDescription)] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(tab, cancellationToken);

        var address = Parse.PageAddress(page);

        var extent = address.PageId / 8;

        var builder = new StringBuilder();

        builder.AppendLine($"Page {Location(address)} in {context.Database?.Name}, extent {extent} " +
                           $"(pages {extent * 8} to {(extent * 8) + 7})");

        var role = GetSystemPageRole(address);

        if (role is not null)
        {
            builder.AppendLine($"System page: {role}");
        }

        var pfsKnown = context.Pfs.TryGetValue(address.FileId, out var pfs);

        if (pfsKnown)
        {
            builder.AppendLine(PfsLine(pfs!.GetPageStatus(address.PageId)));
        }
        else
        {
            builder.AppendLine($"PFS: file {address.FileId} is not loaded");
        }

        builder.AppendLine($"GAM: {MapLine(context.Gam, address.FileId, extent, true, "extent allocated", "extent free")}");

        builder.AppendLine($"SGAM: {MapLine(context.SGam, address.FileId, extent, false, "mixed extent with free pages", "no")}");

        var changed = MapLine(context.Dcm, address.FileId, extent, false, "changed since the last full backup", "unchanged");

        var bulkChanged = MapLine(context.Bcm, address.FileId, extent, false, "bulk changed since the last log backup", "unchanged");

        builder.AppendLine($"DCM: {changed}");

        builder.AppendLine($"BCM: {bulkChanged}");

        var unit = context.FindOwner(address);

        if (unit is not null)
        {
            builder.AppendLine($"Owner: {Describe(unit)} (allocation unit {unit.AllocationUnitId}, {unit.IndexType}, " +
                               $"{unit.AllocationUnitType}, partition {unit.PartitionNumber})");

            builder.AppendLine($"  first IAM {Location(unit.FirstIamPage)}, root {Location(unit.RootPage)}, " +
                               $"first {Location(unit.FirstPage)}");
        }
        else if (pfsKnown && !pfs!.GetPageStatus(address.PageId).IsAllocated)
        {
            builder.AppendLine("Owner: none, the page is not allocated");
        }
        else
        {
            builder.AppendLine("Owner: not found in any IAM of an allocation unit");
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "allocations_extents", ReadOnly = true)]
    [Description("The page ranges an allocation unit owns, per file, from its IAM chain: whole extents as ranges and the " +
                 "single page slots of a mixed extent allocation")]
    public async Task<string> Extents(
        [Description("Allocation unit id from allocations_objects, or a name matched against the schema, table and index name")]
        string unit,
        [Description("Only this file id")] int? file = null,
        [Description("Most ranges to list per file")] int limit = 100,
        [Description(TabDescription)] string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(tab, cancellationToken);

        var allocationUnit = Find(context.AllocationUnits, unit);

        var builder = new StringBuilder();

        builder.AppendLine($"{Describe(allocationUnit)}: allocation unit {allocationUnit.AllocationUnitId}, " +
                           $"{allocationUnit.IndexType}, {allocationUnit.AllocationUnitType}, " +
                           $"partition {allocationUnit.PartitionNumber}");

        builder.AppendLine($"{Format.Number(allocationUnit.TotalPages)} pages allocated, " +
                           $"{Format.Number(allocationUnit.UsedPages)} used; {allocationUnit.IamChain.Pages.Count} IAM pages " +
                           $"in the chain starting at {Location(allocationUnit.FirstIamPage)}");

        var singles = allocationUnit.IamChain.SinglePageSlots.Where(s => s != PageAddress.Empty).ToList();

        if (singles.Count > 0)
        {
            builder.AppendLine($"Single page slots: {string.Join(", ", singles.Select(Location))}");
        }

        var fileIds = context.Files.Select(f => f.FileId).Where(f => file is null || f == file).OrderBy(f => f);

        foreach (var fileId in fileIds)
        {
            var ranges = allocationUnit.IamChain.GetAllocatedPageRanges(fileId).ToList();

            if (ranges.Count == 0)
            {
                continue;
            }

            var pages = ranges.Sum(r => (long)(r.To - r.From + 1));

            builder.AppendLine($"File {fileId}: {ranges.Count} ranges, {Format.Number(pages)} pages");

            foreach (var range in ranges.Take(limit))
            {
                builder.AppendLine(range.From == range.To
                                       ? $"  {fileId}:{range.From}"
                                       : $"  {fileId}:{range.From}-{range.To} ({range.To - range.From + 1} pages)");
            }

            builder.AppendIfNotEmpty(Format.More(ranges.Count, limit, "use limit"));
        }

        return builder.ToString();
    }

    private static string Location(PageAddress address)
        => address == PageAddress.Empty ? "none" : $"{address.FileId}:{address.PageId}";

    private static string PfsLine(PfsByte status)
    {
        var parts = new List<string>
        {
            status.IsAllocated ? "allocated" : "not allocated",
            $"{SpaceFreeText(status.PageSpaceFree)} full"
        };

        if (status.IsIam)
        {
            parts.Add("IAM page");
        }

        if (status.IsMixed)
        {
            parts.Add("mixed extent");
        }

        if (status.GhostRecords)
        {
            parts.Add("has ghost records");
        }

        return $"PFS: {string.Join(", ", parts)} (0x{status.Value:X2})";
    }

    private static string SpaceFreeText(SpaceFree spaceFree)
        => spaceFree switch
        {
            SpaceFree.Empty => "0%",
            SpaceFree.FiftyPercent => "50%",
            SpaceFree.EightyPercent => "80%",
            SpaceFree.NinetyFivePercent => "95%",
            SpaceFree.OneHundredPercent => "100%",
            _ => "unknown"
        };

    private static string MapLine(IReadOnlyDictionary<int, AllocationChain> maps,
                                  short fileId,
                                  int extent,
                                  bool invert,
                                  string whenSet,
                                  string whenClear)
    {
        if (!maps.TryGetValue(fileId, out var chain))
        {
            return "not loaded";
        }

        var set = chain.IsExtentAllocated(extent, fileId, invert);

        return set ? $"set, {whenSet}" : $"clear, {whenClear}";
    }

    private static string? GetSystemPageRole(PageAddress address)
    {
        var page = address.PageId;

        if (page == 0)
        {
            return "file header";
        }

        if (address.FileId == 1 && page == 9)
        {
            return "database boot page";
        }

        if (page == PfsPage.FirstPfsPage || (page > 8 && page % PfsPage.PfsInterval == 0))
        {
            return "PFS page";
        }

        return (page % AllocationPage.AllocationPageCount) switch
        {
            AllocationPage.FirstGamPage => "GAM page",
            AllocationPage.FirstSgamPage => "SGAM page",
            AllocationPage.FirstDcmPage => "DCM page",
            AllocationPage.FirstBcmPage => "BCM page",
            _ => null
        };
    }

    private Task<AllocationsContext> GetContextAsync(string? tab, CancellationToken cancellationToken)
        => Documents.GetContextAsync<AllocationsContext>(tab, cancellationToken);
}
