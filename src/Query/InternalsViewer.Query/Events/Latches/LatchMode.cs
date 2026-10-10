using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.Latches;

// ReSharper disable InconsistentNaming
public enum LatchMode
{
    [EventItemName("")]
    NL = 0,

    [EventItemName("Keep")]
    KP = 1,

    [EventItemName("Shared")]
    SH = 2,

    [EventItemName("Update")]
    UP = 3,

    [EventItemName("Exclusive")]
    EX = 4,

    [EventItemName("Destroy")]
    DT = 5
}
