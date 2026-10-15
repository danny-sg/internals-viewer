using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models;

public sealed record ResultDetails(string Title, IReadOnlyList<ResultDetail> Columns, string? Message = null);
