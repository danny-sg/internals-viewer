using InternalsViewer.Execution.AccessPaths.Values;

namespace InternalsViewer.Execution.Interfaces.AccessPaths.Binding;

/// <summary>
/// Supplies column values for the row currently being examined
/// </summary>
public interface IRowValueSource
{
    /// <summary>
    /// Gets the value for a column ordinal, or a null value if the column is not present
    /// </summary>
    AccessValue GetValue(int ordinal, string? columnName = null);
}
