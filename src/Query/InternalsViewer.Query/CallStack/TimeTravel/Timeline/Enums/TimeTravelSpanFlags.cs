namespace InternalsViewer.Query.CallStack.TimeTravel.Timeline.Enums;

[Flags]
public enum TimeTravelSpanFlags : byte
{
    None = 0,
    Returned = 1,
    StartUnknown = 2
}
