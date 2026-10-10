namespace InternalsViewer.Query.Parsing.Statements;

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
