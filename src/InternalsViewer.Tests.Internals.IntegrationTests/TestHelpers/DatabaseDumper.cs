using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Readers.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.Tests.Internals.IntegrationTests.TestHelpers;

public class DatabaseDumper
{
    public Task DumpDatabase(string databaseName, string outputFolder)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection { ConnectionString = connectionString, DatabaseName = databaseName };

        var reader = new DatabasePageReader(connection);
        
        return Task.CompletedTask;
    }
}
