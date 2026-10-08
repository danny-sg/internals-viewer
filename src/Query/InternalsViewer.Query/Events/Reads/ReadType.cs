namespace InternalsViewer.Query.Events.Reads;

/// <summary>
/// Classifies a page-read group by where the page came from
/// </summary>
public enum ReadType
{
    /// <summary>
    /// Read from disk
    /// </summary>
    NonCached = 0,

    /// <summary>
    /// Read from the buffer pool (cached)
    /// </summary>
    Cached = 1,
}
