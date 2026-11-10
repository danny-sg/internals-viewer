namespace InternalsViewer.Query.FullTrace.Timeline.Tracks.Enums;

public enum TraceTickSourceType
{
    Call,
    RowReturn,
    RowAccess,
    Seek,
    PageFetch,
    Open,
    Watch,
    Evaluate,
    Statement,
    PageHeaderAccess
}
