namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline;

[Flags]
public enum TimeTravelSpanFlags : byte
{
    None = 0,
    Returned = 1,
    StartUnknown = 2
}
