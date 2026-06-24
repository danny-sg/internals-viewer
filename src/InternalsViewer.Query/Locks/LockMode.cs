using System.ComponentModel;

namespace InternalsViewer.Query.Locks;

// ReSharper disable InconsistentNaming
public enum LockMode
{
    [Description("No lock")]
    NL = 0,

    [Description("Schema stability")]
    SCH_S = 1,

    [Description("Schema modification")]
    SCH_M = 2,

    [Description("Shared")]
    S = 3,

    [Description("Update")]
    U = 4,

    [Description("Exclusive")]
    X = 5,

    [Description("Intent shared")]
    IS = 6,

    [Description("Intent update")]
    IU = 7,

    [Description("Intent exclusive")]
    IX = 8,

    [Description("Shared with intent update")]
    SIU = 9,

    [Description("Shared with intent exclusive")]
    SIX = 10,

    [Description("Update with intent exclusive")]
    UIX = 11,

    [Description("Bulk update")]
    BU = 12,

    [Description("Range shared-shared")]
    RS_S = 13,

    [Description("Range shared-update")]
    RS_U = 14,

    [Description("Range insert-null")]
    RI_NL = 15,

    [Description("Range insert-shared")]
    RI_S = 16,

    [Description("Range insert-update")]
    RI_U = 17,

    [Description("Range insert-exclusive")]
    RI_X = 18,

    [Description("Range exclusive-shared")]
    RX_S = 19,

    [Description("Range exclusive-update")]
    RX_U = 20,

    [Description("Last mode sentinel")]
    LAST_MODE = 21
}