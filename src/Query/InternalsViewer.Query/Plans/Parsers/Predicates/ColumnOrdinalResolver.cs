using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.Query.Plans.Parsers.Predicates;

/// <summary>
/// Resolves a showplan column reference to the ordinal it occupies in the index or table
/// </summary>
internal delegate int? ColumnOrdinalResolver(ColumnReference column);