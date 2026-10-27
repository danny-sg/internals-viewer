using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using InternalsViewer.Mcp.Guides;
using InternalsViewer.Mcp.Interfaces.Guides;
using InternalsViewer.UI.App.Controls.Query.Instructions;

namespace InternalsViewer.UI.App.Services.Mcp;

public sealed partial class InstructionsGuideSource : IMcpGuideSource
{
    private static readonly string[] Pages =
    [
        "GettingStarted", "SqlEditor", "Timeline", "Events", "Allocations", "ExecutionPlan", "CallStack", "LogRecords", "FullTrace",
        "Simulation"
    ];

    public IEnumerable<McpGuide> GetGuides()
    {
        yield return new McpGuide("app",
                                  "The app's views, bands, buttons and options as the user sees them, from its own instruction pages",
                                  Read);
    }

    internal static string Flatten(string markdown) => LinkPattern().Replace(TickPattern().Replace(markdown, string.Empty), "**$1**");

    private static string Read()
    {
        var pages = Pages.Select(InstructionsPageProvider.GetPage).Where(p => p is not null).Select(p => Flatten(p!).Trim());

        return string.Join(Environment.NewLine + Environment.NewLine, pages);
    }

    [GeneratedRegex(@"\[\{\{\w+\}\}\] ")]
    private static partial Regex TickPattern();

    [GeneratedRegex(@"\[([^\]]+)\]\((?:view|option|guide):[^)]*\)")]
    private static partial Regex LinkPattern();
}
