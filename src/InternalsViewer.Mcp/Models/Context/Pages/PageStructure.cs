namespace InternalsViewer.Mcp.Models.Context.Pages;

/// <summary>
/// A part of the page that is selected like a slot but is not a record, such as the page header or the offset table
/// </summary>
/// <remarks>
/// The slot number is negative and differs by page type, so it is read from the context and not assumed.
/// </remarks>
public sealed record PageStructure(int Slot, string Name);
