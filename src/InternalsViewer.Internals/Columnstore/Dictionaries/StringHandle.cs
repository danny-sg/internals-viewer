namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Locates one dictionary entry within a string page
/// </summary>
/// <remarks>
/// Offset is a byte offset into an uncompressed page and a bit offset into a compressed one.
/// </remarks>
public readonly record struct StringHandle(int Offset, int Page);
