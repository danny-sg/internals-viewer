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
        var sql = $@"DECLARE @DatabaseName sysname = '{databaseName}'"
    }
}
