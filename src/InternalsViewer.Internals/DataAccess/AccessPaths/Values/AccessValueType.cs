namespace InternalsViewer.Internals.DataAccess.AccessPaths.Values;

/// <summary>
/// Storage strategy used by an <see cref="AccessValue"/>
/// </summary>
public enum AccessValueKind : byte
{
    Null,
    Integer,
    Real,
    Decimal,
    Bytes
}
