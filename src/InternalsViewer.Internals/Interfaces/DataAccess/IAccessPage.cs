using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Internals.Interfaces.DataAccess;

/// <summary>
/// A page as seen by an access path, exposing slot access for a single page
/// </summary>
/// <remarks>
/// Implemented by heap data pages, which have no keys. Pages belonging to an index implement <see cref="IIndexAccessPage"/>, which adds
/// key ordering and child page navigation.
///
/// Moving between pages is the responsibility of the caller, so this contract describes one page in isolation.
/// </remarks>
public interface IAccessPage
{
    PageAddress PageAddress { get; }

    /// <summary>
    /// Index level, where zero is the leaf. A heap data page is always level zero
    /// </summary>
    byte Level { get; }

    bool IsLeaf { get; }

    bool IsRoot { get;  }

    int SlotCount { get; }

    /// <summary>
    /// Gets the underlying record for a slot, used when evaluating residual predicates
    /// </summary>
    IRecord GetRecord(int slot);
}
