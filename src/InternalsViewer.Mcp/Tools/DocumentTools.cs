using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Documents;
using InternalsViewer.Mcp.Interfaces.Tabs;
using InternalsViewer.Mcp.Models.Context;
using InternalsViewer.Mcp.Models.Context.Allocations;
using InternalsViewer.Mcp.Models.Context.Columnstore;
using InternalsViewer.Mcp.Models.Context.Indexes;
using InternalsViewer.Mcp.Models.Context.Pages;
using InternalsViewer.Query.FullTrace;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class DocumentTools(McpDocumentResolver documents)
{
    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "get_selection", ReadOnly = true)]
    [Description("What the user has selected in a tab. A Query tab reports the event, plan node, call tree node, playhead " +
                 "position and the visible views. The other areas report their page, slot, file or filter")]
    public async Task<string> GetSelection(
        [Description("Tab id from list_tabs; the active tab when omitted")] string? document = null,
        CancellationToken cancellationToken = default)
    {
        var tab = await Documents.ResolveTabAsync(document, cancellationToken);

        if (tab is not IMcpDocument)
        {
            return await DescribeAreaSelectionAsync(tab, cancellationToken);
        }

        var scope = await Documents.ResolveAsync(tab.Id, cancellationToken);

        var selection = scope.Selection;

        var builder = new StringBuilder();

        builder.AppendLine($"Document {scope.Query.TabId}: {scope.Query.Title}");

        if (selection.VisibleViews.Length > 0)
        {
            builder.AppendLine($"Visible views: {string.Join(", ", selection.VisibleViews)}");
        }

        if (selection.PlanNode is { } planNode)
        {
            var target = string.Join(".", new[] { planNode.Schema, planNode.Table, planNode.Index }.Where(p => !string.IsNullOrEmpty(p)));

            builder.AppendLine($"Plan node: [{planNode.NodeId}] {planNode.PhysicalOperator} ({planNode.LogicalOperator}) {target}");
        }

        if (selection.Event is { } engineEvent)
        {
            builder.AppendLine($"Event: {Format.EventLine(scope.Context, engineEvent, scope.CropStartUs)}");
        }

        if (selection.CallNode is { } callNode)
        {
            builder.AppendLine($"Call tree node: {Format.NodeLine(scope.Context, callNode)}");

            builder.AppendLine($"  called from: {Format.CallPath(scope.Context, callNode, 8)}");
        }

        if (selection.Playhead is { } playhead)
        {
            builder.AppendLine($"Playhead: {Format.Position(playhead)}");
        }

        if (selection.PlayheadTimeUs is { } timeUs)
        {
            builder.AppendLine($"Timeline position: {Format.Relative(timeUs, scope.CropStartUs)} ms");
        }

        if (selection.TraceRange is { } range)
        {
            builder.AppendLine(range is { From: { } from, To: { } to }
                                   ? $"Trace timeline shows {Format.Position(from)} to {Format.Position(to)} ({range.Axis} axis)"
                                   : $"Trace timeline shows {range.Start:N0} to {range.End:N0} on the {range.Axis} axis");
        }

        if (selection.EventRange is { } window)
        {
            builder.AppendLine($"Events timeline shows {Format.Relative(window.StartUs, scope.CropStartUs)} ms to " +
                               $"{Format.Relative(window.EndUs, scope.CropStartUs)} ms");
        }

        if (selection.Scope is { } scoped)
        {
            builder.AppendLine($"Scope handles: {Format.Relative(scoped.StartUs, scope.CropStartUs)} ms to " +
                               $"{Format.Relative(scoped.EndUs, scope.CropStartUs)} ms");
        }

        foreach (var page in selection.Pages)
        {
            builder.AppendLine(PageLine(page));
        }

        if (selection.IsEmpty)
        {
            builder.AppendLine("Nothing is selected.");
        }

        return builder.ToString();
    }

    private static async Task<string> DescribeAreaSelectionAsync(IMcpTab tab, CancellationToken cancellationToken)
    {
        var context = await tab.GetContextAsync(cancellationToken);

        var builder = new StringBuilder();

        builder.AppendLine($"{tab.Area} tab {tab.Id}: {tab.Title}");

        foreach (var line in DescribeContext(context))
        {
            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    private static IEnumerable<string> DescribeContext(TabContext context)
    {
        switch (context)
        {
            case AllocationsContext allocations:
                yield return $"File {allocations.FileId}";

                if (!string.IsNullOrEmpty(allocations.Filter))
                {
                    yield return $"Filter: {allocations.Filter}";
                }

                if (allocations.Overlay is { } overlay)
                {
                    yield return $"Overlay: {overlay}";
                }

                yield return allocations.ShowsSystemObjects ? "System objects shown" : "System objects hidden";

                if (allocations.SelectedObjects.Count > 0)
                {
                    yield return $"Selected objects: {string.Join(", ", allocations.SelectedObjects)}";
                }

                break;

            case PageContext page:
                yield return page.Page is null
                    ? "No page is open."
                    : $"Page {page.Address} {page.Page.PageHeader.PageTypeName}";

                if (page.CurrentSlot is { } slot)
                {
                    var structure = page.Structures.FirstOrDefault(s => s.Slot == slot);

                    yield return structure is null
                        ? $"Slot {slot} selected"
                        : $"{structure.Name} selected (slot {slot})";
                }

                if (page.Structures.Count > 0)
                {
                    yield return "Structures selectable as slots: " +
                                 string.Join(", ", page.Structures.Select(s => $"{s.Name} = {s.Slot}"));
                }

                if (page.SelectedName is { } name)
                {
                    yield return $"Selected {name} = {page.SelectedValue} at offset {page.SelectionStart}, " +
                                 $"{page.Selection?.Length ?? 0} bytes";
                }

                yield return $"Marker view: {page.View}";

                break;

            case IndexContext index:
                yield return $"Root page {index.RootPage}, {index.Nodes.Count} pages loaded";

                if (index.SelectedNode is { } node)
                {
                    yield return $"Page {node.PageAddress} {node.PageType} height {node.IndexLevel} selected";
                }

                if (index.SelectedSlot is { } selectedSlot)
                {
                    yield return $"Slot {selectedSlot} selected";
                }

                break;

            case ColumnstoreContext columnstore:
                yield return columnstore.Index is { } columnStoreIndex
                    ? $"{columnstore.AllocationUnit?.IndexName}: {columnStoreIndex.RowGroups.Count} row groups"
                    : "Index not loaded yet.";

                yield return $"Open documents: {string.Join(", ", columnstore.Documents)}";

                if (columnstore.CurrentDocument is { } document)
                {
                    yield return $"Showing {document}" + (columnstore.Region is { } region ? $", region {region}" : string.Empty);
                }

                yield return $"Metadata list: {columnstore.MetadataTab}";

                if (columnstore.AvailableRegions.Count > 0)
                {
                    yield return $"Regions: {string.Join(", ", columnstore.AvailableRegions)}";
                }

                if (columnstore.SelectedMarker is { } marker)
                {
                    yield return $"Marker {marker} selected" +
                                 (columnstore.SelectedMarkerValue is { Length: > 0 } value ? $" = {value}" : string.Empty);
                }

                if (columnstore.MarkerNames.Count > 0)
                {
                    yield return $"Markers: {string.Join(", ", columnstore.MarkerNames.Take(40))}";
                }

                if (columnstore.SelectedRow is { } row)
                {
                    yield return $"Row {row} selected";
                }

                break;
        }
    }

    private static string PageLine(McpPageSelection page)
    {
        var parts = new List<string> { $"Page {page.Address}{(page.IsActive ? " (active)" : string.Empty)}" };

        if (page.Slot is { } slot)
        {
            parts.Add($"slot {slot}");
        }

        if (page.Field is { } field)
        {
            parts.Add(page.FieldOffset is { } offset ? $"field {field} at offset {offset}" : $"field {field}");
        }

        if (page.LogRecord is { } logRecord)
        {
            parts.Add($"log record {logRecord}");
        }

        return string.Join(", ", parts);
    }
}
