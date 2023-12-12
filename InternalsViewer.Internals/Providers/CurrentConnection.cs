using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Providers;

public class CurrentConnection(string connectionString, string databaseName)
{
    public string ConnectionString { get; } = connectionString;

    public string DatabaseName { get; } = databaseName;
}
