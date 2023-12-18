namespace InternalsViewer.Internals.Engine.Database.Enums;

/// <summary>
/// Allocation Unit Types
/// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-allocation-units-transact-sql"/>
/// </summary>
public enum AllocationUnitType : byte
{
    Dropped = 0,

    /// <summary>
    /// In-row data (all data types, except LOB data types)
    /// </summary>
    InRowData = 1,

    /// <summary>
    /// Large object (LOB) data (text, ntext, image, xml, large value types, and CLR user-defined types)
    /// </summary>
    LargeObjectData = 2,

    RowOverflowData = 3
}