namespace InternalsViewer.Query.FullTrace.Timeline.Enums;

[Flags]
public enum FullTraceSpanFlags : byte
{
    None = 0,
    Returned = 1,
    StartUnknown = 2
}
