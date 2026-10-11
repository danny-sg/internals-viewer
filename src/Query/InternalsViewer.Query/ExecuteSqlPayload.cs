using InternalsViewer.Query.Parsing.Statements;

namespace InternalsViewer.Query;

public sealed record ExecuteSqlPayload(string SqlText,
    QueryOptions QueryOptions,
    StatementType StatementType,
    TrackedSelectionRange? TrackedSelection);