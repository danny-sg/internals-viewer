using InternalsViewer.Internals.DataAccess.AccessPaths.Values;

namespace InternalsViewer.Internals.Interfaces.DataAccess;

/// <summary>
/// Supplies column values for the row currently being examined
/// </summary>
public interface IRowValueSource
{
    /// <summary>
    /// Gets the value for a column ordinal, or a null value if the column is not present
    /// </summary>
    AccessValue GetValue(int ordinal);
}
