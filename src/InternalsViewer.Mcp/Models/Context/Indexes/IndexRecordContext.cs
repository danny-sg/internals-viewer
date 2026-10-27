namespace InternalsViewer.Mcp.Models.Context.Indexes;

/// <summary>
/// A record of the page selected in an Index tab's details pane
/// </summary>
public sealed record IndexRecordContext(int Slot, string? DownPage, IReadOnlyList<string> Values);