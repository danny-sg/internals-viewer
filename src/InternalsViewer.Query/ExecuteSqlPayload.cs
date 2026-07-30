using InternalsViewer.Query.Parsing;

namespace InternalsViewer.Query;

public sealed record ExecuteSqlPayload(string SqlText,
    QueryOptions QueryOptions,
    StatementType StatementType,
    TrackedSelectionRange? TrackedSelection);