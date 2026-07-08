using System.ComponentModel;

namespace InternalsViewer.Query.Events.Latches;

// ReSharper disable InconsistentNaming
public enum LatchMode
{
    [Description("Null")]
    NL = 0,

    [Description("Keep")]
    KP = 1,

    [Description("Shared")]
    SH = 2,

    [Description("Update")]
    UP = 3,

    [Description("Exclusive")]
    EX = 4,

    [Description("Destroy")]
    DT = 5
}
