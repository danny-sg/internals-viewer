using System.Data;

namespace InternalsViewer.Execution.Common.AccessPaths.Definitions;

public sealed record OutputColumn(string Name, string? Table = null, SqlDbType? DataType = null);
