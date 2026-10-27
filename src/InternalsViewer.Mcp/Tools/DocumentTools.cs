using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Query.FullTrace;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools;

[McpServerToolType]
public sealed class DocumentTools(McpDocumentResolver documents)
{
    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "list_documents", ReadOnly = true, Idempotent = true)]
    [Description("The query documents open in Internals Viewer: their ids, database, whether a run and a Full Trace are loaded, " +
                 "and the active one")]
    public async Task<string> ListDocuments(CancellationToken cancellationToken)
    {
        var all = await Documents.GetDocumentsAsync(cancellationToken);

        if (all.Count == 0)
        {
            return "No query documents are open. Open a database and a Query tab in Internals Viewer first.";
        }

        var table = new TextTable("Id", "Active", "Title", "Server", "Database", "Run", "Full Trace", "Query");

        foreach (var document in all)
        {
            var run = document.Run;

            var sql = run?.SqlText ?? document.SqlText;

            var firstLine = sql.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;

            table.Add(document.Id,
                      document.IsActive ? "yes" : string.Empty,
                      document.Title,
                      document.ServerName,
                      document.DatabaseName,
                      run is null ? "none" : run.Result.IsSuccess ? "yes" : "failed",
                      run?.ExtraOf<TraceResult>() is null ? string.Empty : "yes",
                      Format.Truncate(firstLine, 60));
        }

        var builder = new StringBuilder();

        table.Write(builder);

        builder.AppendLine();

        builder.AppendLine("Pass a document id to the other tools, or leave it out for the active document.");

        return builder.ToString();
    }

    [McpServerTool(Name = "get_selection", ReadOnly = true)]
    [Description("What the user has selected in a query document: the event, plan node, call tree node, playhead position " +
                 "and the visible views")]
    public async Task<string> GetSelection(
        [Description("Document id from list_documents; the active document when omitted")] string? document = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await Documents.ResolveAsync(document, cancellationToken);

        var selection = await scope.RequireNavigator().GetSelectionAsync(cancellationToken);

        var builder = new StringBuilder();

        builder.AppendLine($"Document {scope.Document.Id}: {scope.Document.Title}");

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
