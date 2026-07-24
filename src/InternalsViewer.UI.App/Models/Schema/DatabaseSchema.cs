using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Schema;

public sealed class DatabaseSchema
{
    public List<SqlTable> Tables { get; set; } = new();

    public List<SqlColumn> Columns { get; set; } = new();

    public List<SqlSchema> Schemas { get; set; } = new();
}

public sealed class SqlSchema
{
    public string Name { get; set; } = string.Empty;
}

public sealed class SqlTable
{
    public string Name { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;
}

public sealed class SqlColumn
{
    public string Name { get; set; } = string.Empty;

    public string Table { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;
}