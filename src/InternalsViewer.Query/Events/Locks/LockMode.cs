using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.Events.Locks;

// ReSharper disable InconsistentNaming
public enum LockMode
{
    [EventItemName("No lock")]
    NL = 0,

    [EventItemName("Schema stability")]
    SCH_S = 1,

    [EventItemName("Schema modification")]
    SCH_M = 2,

    [EventItemName("Shared")]
    S = 3,

    [EventItemName("Update")]
    U = 4,

    [EventItemName("Exclusive")]
    X = 5,

    [EventItemName("Intent shared")]
    IS = 6,

    [EventItemName("Intent update")]
    IU = 7,

    [EventItemName("Intent exclusive")]
    IX = 8,

    [EventItemName("Shared with intent update")]
    SIU = 9,

    [EventItemName("Shared with intent exclusive")]
    SIX = 10,

    [EventItemName("Update with intent exclusive")]
    UIX = 11,

    [EventItemName("Bulk update")]
    BU = 12,

    [EventItemName("Range shared-shared")]
    RS_S = 13,

    [EventItemName("Range shared-update")]
    RS_U = 14,

    [EventItemName("Range insert-null")]
    RI_NL = 15,

    [EventItemName("Range insert-shared")]
    RI_S = 16,

    [EventItemName("Range insert-update")]
    RI_U = 17,

    [EventItemName("Range insert-exclusive")]
    RI_X = 18,

    [EventItemName("Range exclusive-shared")]
    RX_S = 19,

    [EventItemName("Range exclusive-update")]
    RX_U = 20,

    [EventItemName("Last mode sentinel")]
    LAST_MODE = 21
}

// ReSharper disable IdentifierTypo