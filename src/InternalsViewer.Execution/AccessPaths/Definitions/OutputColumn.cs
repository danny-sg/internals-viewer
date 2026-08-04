using System.Data;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

public sealed record OutputColumn(string Name, string? Table = null, SqlDbType? DataType = null);
