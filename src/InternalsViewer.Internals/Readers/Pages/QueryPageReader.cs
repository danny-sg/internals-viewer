using System.Data;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using InternalsViewer.Internals.Providers;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Page Reader for reading a page using a online database with DBCC PAGE
/// </summary>
public class QueryPageReader(string connectionString) : PageReader, IPageReader
{
    private const int ParentObjectIndex = 0;
    private const int ObjectIndex = 1;
    private const int FieldIndex = 2;
    private const int ValueIndex = 3;

    private const int DbccPageHexDumpOption = 2;

    private const string DbccPageCommand = @"DBCC PAGE({0}, {1}, {2}, {3}) WITH TABLERESULTS";

    /// <summary>
    /// Loads the database page using DBCC PAGE (hex dump)
    /// </summary>
    public async Task<byte[]> Read(string name, PageAddress pageAddress)
    {
        var pageCommand = string.Format(DbccPageCommand,
                                        name,
                                        pageAddress.FileId,
                                        pageAddress.PageId,
                                        DbccPageHexDumpOption);
        var offset = 0;
        var data = new byte[PageData.Size];

        await using var connection = new SqlConnection(connectionString);

        await using var command = new SqlCommand(pageCommand, connection);

        command.CommandType = CommandType.Text;

        try
        {
            await connection.OpenAsync();

            var reader = await command.ExecuteReaderAsync();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    var parentObject = reader.GetString(ParentObjectIndex);
                    var objectName = reader.GetString(ObjectIndex);
                    var value = reader.GetString(ValueIndex);

                    if (parentObject == "DATA:"
                        && objectName.StartsWith("Memory Dump"))
                    {
                        offset = ReadData(value, offset, data);
                    }
                }

                reader.Close();
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error reading page {pageAddress.FileId}:{pageAddress.PageId}", ex);
        }

        return data;
    }
}