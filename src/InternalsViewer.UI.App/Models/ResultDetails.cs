using System.Collections.Generic;
using InternalsViewer.Query.Results;

namespace InternalsViewer.UI.App.Models;

public sealed record ResultDetails(string Title, IReadOnlyList<ResultDetail> Columns, string? Message = null)
{
    public QueryResultSet? ResultSet { get; init; }
}
