namespace InternalsViewer.Mcp.Models.Context.Queries;

/// <summary>
/// A menu toggle in a Query tab
/// </summary>
public sealed record McpOption(string Key, string Label, string Group, bool IsOn, bool IsEnabled);
