using InternalsViewer.Query.Server;

namespace InternalsViewer.Query.Tests;

public class SqlServerProcessTests
{
    [Theory]
    [InlineData("", "MSSQLSERVER")]
    [InlineData("SQL2025", "MSSQL$SQL2025")]
    public void ServiceName_Follows_The_Instance_Name(string instanceName, string serviceName)
    {
        var process = new SqlServerProcess(1234, Environment.MachineName, instanceName);

        Assert.Equal(serviceName, process.ServiceName);
    }
}
