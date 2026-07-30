using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Interfaces.DataAccess;

/// <summary>
/// A page belonging to an index, where slots are ordered by key
/// </summary>
/// <remarks>
/// Implemented by index pages at any level, including clustered index leaf pages. Key ordering is intrinsic to a B-tree page, so a page
/// either implements this contract or it does not - there is no page that is conditionally keyed.
/// </remarks>
public interface IIndexPageAccessor : IRowPageAccessor
{
    bool IsRoot { get; }

    /// <summary>
    /// The next page at the leaf level, following the page's linked list
    /// </summary>
    PageAddress NextPage { get; }

    PageAddress PreviousPage { get; }

    /// <summary>
    /// Gets the key for a slot
    /// </summary>
    AccessKey GetKey(int slot);

    /// <summary>
    /// Compares the key at a slot against a target key, considering only the leading columns
    /// </summary>
    int CompareKeyPrefix(int slot, in AccessKey target, int width);

    /// <summary>
    /// Gets the child page pointed to by a non leaf slot
    /// </summary>
    /// <exception cref="System.NotSupportedException">The page is a leaf</exception>
    PageAddress GetChildPage(int slot);
}
