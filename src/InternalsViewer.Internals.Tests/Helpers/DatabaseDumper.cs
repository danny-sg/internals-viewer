using System.Data;
using InternalsViewer.Internals.Engine.Pages.Enums;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Tests.Helpers;

public class DatabaseDumper(ITestOutputHelper testOutputHelper)
{
    public ITestOutputHelper TestOutputHelper { get; } = testOutputHelper;

    public static readonly string DbccPageCommand = @"DBCC PAGE({0}, {1}, {2}, {3}) WITH TABLERESULTS";

    [Theory]
    [InlineData("TestDatabase", @"./Test Data")]
    public async Task DumpDatabase(string databaseName, string outputFolder)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString("local");

        await using var connection = new SqlConnection(connectionString);

        var path = Path.Combine(outputFolder, databaseName);

        // delete existing files
        
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }

        Directory.CreateDirectory(path);

        await connection.OpenAsync();

        await connection.ChangeDatabaseAsync(databaseName);

        var pageCount = await GetPageCount(connection);

        for (var i = 1; i < pageCount; i++)
        {
            var pageAddress = new PageAddress(1, i);

            await DumpPage(connection, pageAddress, path);
        }
    }

    private async Task<int> GetPageCount(SqlConnection connection)
    {
        await using var command = new SqlCommand("SELECT size FROM sys.database_files WHERE type_desc = 'ROWS'", connection);

        var result = await command.ExecuteScalarAsync() as int?;

        return result ?? 0;
    }

    public async Task DumpPage(SqlConnection connection, PageAddress pageAddress, string outputFolder)
    {
        var filename = string.Format("{0}_{1}_{2}_{3}.page",
                                     connection.Database,
                                     pageAddress.FileId,
                                     pageAddress.PageId,
                                     "Temp");

        var fullName = Path.Combine(outputFolder, filename);

        TestOutputHelper.WriteLine($"Dumping {pageAddress}");

        await using var outputFile = new StreamWriter(fullName, false);

        var pageCommand = string.Format(DbccPageCommand,
                                        connection.Database,
                                        pageAddress.FileId,
                                        pageAddress.PageId,
                                        2);

        await using var command = new SqlCommand(pageCommand, connection);

        command.CommandType = CommandType.Text;

        var reader = await command.ExecuteReaderAsync();

        var pageType = PageType.None;

        if (reader.HasRows)
        {
            while (reader.Read())
            {
                var parentObject = reader.GetString(0);
                var objectValue = reader.GetString(1);
                var field = reader.GetString(2);
                var value = reader.GetString(3);

                if (parentObject == "PAGE HEADER:" && field == "m_type")
                {
                    pageType = (PageType)Enum.Parse(typeof(PageType), value);
                }
                else if (parentObject == "DATA:" && objectValue.StartsWith("Memory Dump"))
                {
                    await outputFile.WriteLineAsync(value.PadRight(64)[20..64]);
                }
            }

            reader.Close();
        }

        await outputFile.FlushAsync();
        outputFile.Close();

        var newFilename = string.Format("{0}_{1}_{2}_{3}.page",
                                        connection.Database,
                                        pageAddress.FileId,
                                        pageAddress.PageId,
                                        pageType);

        var newFullName = Path.Combine(outputFolder, newFilename);

        File.Move(fullName, newFullName);

        TestOutputHelper.WriteLine($"--> {newFullName}");
    }
}
