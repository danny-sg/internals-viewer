namespace InternalsViewer.Internals.Engine.Indexes.Enums;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
[Flags]
public enum RangeOptions
{
    DBRANGE_INCLUSIVEEND = 0,
    DBRANGE_EXCLUSIVESTART = 1,
    DBRANGE_EXCLUSIVEEND = 2,
    DBRANGE_EXCLUDENULLS = 4,
    DBRANGE_PREFIX = 8,
    DBRANGE_MATCH = 16
}
