using System;

namespace InternalsViewer.UI.App.Models.Query;

/// <summary>
/// A query that has been executed against a database
/// </summary>
public sealed record QueryHistoryEntry(string Sql, DateTimeOffset ExecutedAt);
