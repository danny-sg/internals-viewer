namespace InternalsViewer.Internals.Engine.Database.Enums;

/// <summary>
/// Index Types
/// </summary>
/// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-indexes-transact-sql"/>
public enum IndexType : byte
{
    Heap = 0,
    Clustered = 1,
    NonClustered = 2,
    Xml = 3,
    Spatial = 4,
    ClusteredColumnStore = 5,
    NonClusteredColumnStore = 6,
    NonClusteredHash = 7
}