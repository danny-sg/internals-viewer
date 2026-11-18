namespace InternalsViewer.Query.Plans.Values.Enums;

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
