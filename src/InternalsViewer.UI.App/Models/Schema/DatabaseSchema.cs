using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.UI.App.Models.Schema;

public class DatabaseSchema
{
    public List<SqlTable> Tables { get; set; } = new();

    public List<SqlColumn> Columns { get; set; } = new();

    public List<SqlSchema> Schemas { get; set; } = new();
}

public class SqlSchema
{
    public string Name { get; set; } = string.Empty;
}

public class SqlTable
{
    public string Name { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;
}

public class SqlColumn
{
    public string Name { get; set; } = string.Empty;

    public string Table { get; set; } = string.Empty;

    public string Schema { get; set; } = string.Empty;
}