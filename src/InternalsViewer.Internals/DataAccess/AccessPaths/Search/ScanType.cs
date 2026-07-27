namespace InternalsViewer.Internals.DataAccess.AccessPaths.Search;

/// <summary>
/// Classification of an access path, matching the operators found in an execution plan
/// </summary>
public enum ScanType
{
    TableScan,
    ClusteredIndexScan,
    NonClusteredIndexScan,
    ClusteredIndexSeek,
    NonClusteredIndexSeek,
    KeyLookup,
    RidLookup
}
