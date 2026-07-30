namespace InternalsViewer.Execution.AccessPaths.Values;

/// <summary>
/// Storage strategy used by an <see cref="AccessValue"/>
/// </summary>
public enum AccessValueType : byte
{
    Null,
    Integer,
    Real,
    Decimal,
    Bytes
}
