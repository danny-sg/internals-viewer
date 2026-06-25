using System.Data;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Readers.Pages;

/// <summary>
/// Page Reader for reading a page using an online database with DBCC PAGE
/// </summary>
public sealed class QueryPageReader(ILogger<QueryPageReader> logger, string connectionString) 
    : PageReader, IPageReader
{

    private const int ValueIndex = 3;

    private const int DbccPageHexDumpOption = 2;

    /// <summary>
    /// Characters before the hex data on each memory dump line: a 16 character address, a colon and
    /// three spaces (e.g. "00000036061F6000:   ").
    /// </summary>
    private const int HexLinePrefixLength = 20;

    /// <summary>Index of the colon that terminates the address on a memory dump line.</summary>
    private const int AddressColonIndex = 16;

    /// <summary>Number of hex characters consumed from each memory dump line after the prefix.</summary>
    private const int HexLineLength = 44;

    private const string DbccPageCommand = @"DBCC PAGE({0}, {1}, {2}, {3}) WITH TABLERESULTS";

    private SqlConnection Connection { get; } = new(connectionString);

    private ILogger<QueryPageReader> Logger { get; } = logger;

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
            if (Connection.State != ConnectionState.Open)
            {
                await Connection.OpenAsync();
            }

            await using var command = new SqlCommand(pageCommand, Connection);

            command.CommandType = CommandType.Text;

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

            if (reader.HasRows)
            {
                // Reused across every row: prefix (skipped) plus the hex characters we consume.
                var valueBuffer = new char[HexLinePrefixLength + HexLineLength];

                #pragma warning disable VSTHRD103 // Sync Read avoids the per-row Task allocations of ReadAsync
                while (reader.Read())
                {
                    using var valueReader = reader.GetTextReader(ValueIndex);

                    var charsRead = valueReader.ReadBlock(valueBuffer, 0, valueBuffer.Length);

                    var line = valueBuffer.AsSpan(0, charsRead);

                    if (!IsHexDumpLine(line))
                    {
                        continue;
                    }

                    offset = ReadData(line[HexLinePrefixLength..], offset, buffer);
                }
                #pragma warning restore VSTHRD103

                reader.Close();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error reading page {PageAddress}: {Command} ", pageAddress, pageCommand);

            throw new Exception($"Error reading page {pageAddress.FileId}:{pageAddress.PageId}", ex);
        }
    }

    /// <summary>
    /// Identifies a memory dump line by its address prefix (16 hex characters followed by a colon), so the dump rows
    /// can be picked out without reading the ParentObject/Object filter columns.
    /// </summary>
    private static bool IsHexDumpLine(ReadOnlySpan<char> line)
    {
        if (line.Length <= AddressColonIndex || line[AddressColonIndex] != ':')
        {
            return false;
        }

        for (var i = 0; i < AddressColonIndex; i++)
        {
            var c = line[i];

            var isHex = c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}
