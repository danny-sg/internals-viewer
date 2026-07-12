namespace InternalsViewer.Query.Events.Reads;

/// <summary>
/// Classifies a page-read episode by where the page came from
/// </summary>
public enum ReadType
{
    NonCached = 0,
    Cached = 1,
}
