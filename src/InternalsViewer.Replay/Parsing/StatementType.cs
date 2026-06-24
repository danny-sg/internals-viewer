namespace InternalsViewer.Replay.Parsing;

public enum StatementType
{
    Unknown = 0,
    Select,
    Modification,
    MultiStatementSelect,
    MultiStatementModification,
    StoredProcedure,
    Invalid
}
