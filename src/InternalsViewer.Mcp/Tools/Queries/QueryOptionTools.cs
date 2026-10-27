using System.ComponentModel;
using System.Text;
using InternalsViewer.Mcp.Documents;
using InternalsViewer.Mcp.Formatting;
using InternalsViewer.Mcp.Interfaces.Commands;
using InternalsViewer.Mcp.Models.Context.Queries;
using InternalsViewer.Mcp.Tabs;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InternalsViewer.Mcp.Tools.Queries;

[McpServerToolType]
public sealed class QueryOptionTools(McpDocumentResolver documents)
{
    private const string TabDescription = "Query tab id from list_tabs; the active Query tab when omitted";

    private McpDocumentResolver Documents { get; } = documents;

    [McpServerTool(Name = "query_options", ReadOnly = true, Idempotent = true)]
    [Description("The toggles in the Query tab's menus: which views are visible (View), what is captured and shown (Events), " +
                 "Query settings and Full Trace recording (Trace). Each has a key for query_set_option, whether it is on, " +
                 "and whether it can be changed now")]
    public async Task<string> Options([Description(TabDescription)] string? tab = null, CancellationToken cancellationToken = default)
    {
        var context = await Documents.GetContextAsync<QueryContext>(tab, cancellationToken);

        var builder = new StringBuilder();

        builder.AppendLine($"Options of Query tab {context.TabId}");

        foreach (var group in context.Options.GroupBy(o => o.Group))
        {
            builder.AppendLine();

            builder.AppendLine(group.Key);

            var table = new TextTable("Key", "Option", "On", "Changeable");

            foreach (var option in group)
            {
                table.Add(option.Key, option.Label, option.IsOn ? "yes" : "no", option.IsEnabled ? "yes" : "no");
            }

            table.Write(builder);
        }

        return builder.ToString();
    }

    [McpServerTool(Name = "query_set_option")]
    [Description("Turn a Query tab menu toggle on or off, for example full-trace to record a Full Trace on the next run, or " +
                 "call-tree to show the Call Tree. The keys come from query_options. Capture options apply to the next run. " +
                 "Ask the user before turning on full-trace, as it records SQL Server with elevated rights")]
    public async Task<string> SetOption([Description("Option key from query_options")] string key,
                                        [Description("True to turn the option on, false to turn it off")] bool enabled,
                                        [Description(TabDescription)] string? tab = null,
                                        CancellationToken cancellationToken = default)
    {
        var session = await Documents.OpenAsync<QueryContext>(tab, cancellationToken);

        var option = session.Context.Options.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase))
                     ?? throw new McpException(
                         $"No option '{key}'. Keys: {string.Join(", ", session.Context.Options.Select(o => o.Key))}.");

        switch (option.Unavailable)
        {
            case McpOptionUnavailable.FullTraceLoaded:
                throw new McpException($"{option.Label} cannot be changed while a Full Trace is loaded. Open a run without one.");

            case McpOptionUnavailable.NeedsFullTrace:
                throw new McpException(
                    $"{option.Label} needs a Full Trace to be loaded. Turn on full-trace and run the query first.");

            case McpOptionUnavailable.NeedsConsent when enabled:
                throw new McpException(
                    "Full Trace records SQL Server with Time Travel Debugging, and the user has not yet accepted its warning " +
                    "and licence. Ask the user to turn it on once from the Trace menu, then try again.");
        }

        await session.RunAsync<IQueryCommands>(c => c.SetOptionAsync(option.Key, enabled), cancellationToken);

        var current = session.Context.Options.FirstOrDefault(o => o.Key == option.Key) ?? option;

        var state = current.IsOn ? "on" : "off";

        return option.Key == "full-trace"
            ? $"Turned {state} Full Trace. It applies to the next run."
            : $"Turned {state} {option.Label}.";
    }
}
