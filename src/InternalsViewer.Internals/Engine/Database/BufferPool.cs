using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// Set of pages in the server's buffer bool
/// </summary>
public record BufferPool(List<PageAddress> CleanPages, List<PageAddress> DirtyPages)
{
    /// <summary>
    /// Gets the clean page addresses.
    /// </summary>
    public List<PageAddress> CleanPages { get; } = CleanPages;

    /// <summary>
    /// Gets the dirty page addresses.
    /// </summary>
    public List<PageAddress> DirtyPages { get; } = DirtyPages;
}