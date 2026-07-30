namespace InternalsViewer.Query.Parsing.Plans.Predicates;

/// <summary>
/// Resolves a showplan column reference to the ordinal it occupies in the index or table
/// </summary>
public delegate int? ColumnOrdinalResolver(ColumnReference column);