namespace InternalsViewer.Mcp.Models.Context.Queries;

/// <summary>
/// A menu toggle in a Query tab
/// </summary>
public sealed record McpOption(
    string Key,
    string Label,
    string Group,
    bool IsOn,
    McpOptionUnavailable Unavailable = McpOptionUnavailable.None,
    bool IsChoice = false)
{
    public bool IsEnabled => Unavailable == McpOptionUnavailable.None;
}

/// <summary>
/// Why a menu toggle cannot be changed now
/// </summary>
public enum McpOptionUnavailable
{
    None,
    FullTraceLoaded,
    NeedsFullTrace,
    NeedsConsent
}
