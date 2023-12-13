using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InternalsViewer.Internals.Providers;
using InternalsViewer.Internals.Providers.Metadata;
using InternalsViewer.Tests.Internals.IntegrationTests.Helpers;

namespace InternalsViewer.Tests.Internals.IntegrationTests.Providers.Metadata;

public class DatabaseFileInfoProviderTests
{
    [Fact]
    public async Task Can_Get_Database_File_Info()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection(connectionString, "AdventureWorks2022");

        var databaseFileInfoProvider = new DatabaseFileInfoProvider(connection);

        var databaseFileInfo = await databaseFileInfoProvider.GetFiles(connection.DatabaseName);

        Assert.Single(databaseFileInfo);

        var file = databaseFileInfo.First();

        Assert.Equal("AdventureWorks2022", file.Name);
        Assert.Equal("PRIMARY", file.FileGroup);
        Assert.Equal(1, file.FileId);
        Assert.Equal("AdventureWorks2022.mdf", file.FileName);
        Assert.Equal("C:\\Program Files\\Microsoft SQL Server\\MSSQL16.MSSQLSERVER\\MSSQL\\DATA\\AdventureWorks2022.mdf", file.PhysicalName);
        
        Assert.True(file.Size > 1);
        Assert.True(file.TotalExtents > 0);
        Assert.True(file.UsedExtents > 0);
        Assert.True(file.TotalMb > 1);
        Assert.True(file.TotalPages > 1);
        Assert.True(file.UsedPages > 0);
    }

    [Fact]
    public async Task Can_Get_Database_File_Size()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        var connection = new CurrentConnection(connectionString, "AdventureWorks2022");

        var databaseFileInfoProvider = new DatabaseFileInfoProvider(connection);

        var fileSize = await databaseFileInfoProvider.GetFileSize(1);

        Assert.True(fileSize > 1);
    }
}
