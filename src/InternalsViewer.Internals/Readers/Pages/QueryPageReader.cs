using System.Data;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Page Reader for reading a page using an online database with DBCC PAGE
/// </summary>
public sealed class QueryPageReader(ILogger<QueryPageReader> logger, string connectionString) : PageReader, IPageReader
{
    private const int ParentObjectIndex = 0;
    private const int ObjectIndex = 1;
    private const int ValueIndex = 3;

    private const int DbccPageHexDumpOption = 2;

    private const string DbccPageCommand = @"DBCC PAGE({0}, {1}, {2}, {3}) WITH TABLERESULTS";

    private static ReadOnlySpan<char> DataParent => "DATA:";
    private static ReadOnlySpan<char> MemoryDumpPrefix => "Memory Dump";

    private ILogger<QueryPageReader> Logger { get; } = logger;

    private readonly SqlConnection _connection = new(connectionString);

    /// <summary>
    /// Loads the database page using DBCC PAGE (hex dump)
    /// </summary>
    public async Task<byte[]> Read(string name, PageAddress pageAddress)
    {
        var data = new byte[PageData.Size];

        await ReadInto(name, pageAddress, data);

        return data;
    }

    public async Task ReadInto(string name, PageAddress pageAddress, byte[] buffer)
    {
        var pageCommand = string.Format(DbccPageCommand,
                                        name,
                                        pageAddress.FileId,
                                        pageAddress.PageId,
                                        DbccPageHexDumpOption);

        Logger.LogDebug("Reading page {PageAddress}: {CommandSql}", pageAddress, pageCommand);

        var offset = 0;

        try
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await using var command = new SqlCommand(pageCommand, _connection);

            command.CommandType = CommandType.Text;

            await using var reader = await command.ExecuteReaderAsync();

            if (reader.HasRows)
            {
                #pragma warning disable VSTHRD103 // Async read causes string and byte[] allocations
                while (reader.Read())
                {
                    var parentObject = reader.GetString(ParentObjectIndex).AsSpan();
                    var objectName = reader.GetString(ObjectIndex).AsSpan();

                    if (parentObject.SequenceEqual(DataParent) && objectName.StartsWith(MemoryDumpPrefix))
                    {
                        var value = reader.GetString(ValueIndex);

                        var charsRead = Math.Min(44, Math.Max(0, value.Length - 20));

                        offset = ReadData(value.AsSpan(20, charsRead), offset, buffer);
                    }
                }
#pragma warning restore VSTHRD103 // Call async methods when in an async method

                reader.Close();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error reading page {PageAddress}: {Command} ", pageAddress, pageCommand);

            throw new Exception($"Error reading page {pageAddress.FileId}:{pageAddress.PageId}", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
