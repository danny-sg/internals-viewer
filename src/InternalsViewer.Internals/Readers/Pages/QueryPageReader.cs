using System.Data;
using System.Diagnostics;
using System.Threading;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Interfaces.Readers;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Readers.Pages;

#pragma warning disable VSTHRD103 // Sync Read avoids the per-row Task allocations of ReadAsync

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

    /// <summary>
    /// Index of the colon that terminates the address on a memory dump line</summary>
    private const int AddressColonIndex = 16;

    /// <summary>
    /// Number of hex characters consumed from each memory dump line after the prefix
    /// </summary>
    private const int HexLineLength = 44;

    private string ConnectionString { get; } = connectionString;

    private ILogger<QueryPageReader> Logger { get; } = logger;

    /// <summary>
    /// Loads the database page using DBCC PAGE (hex dump)
    /// </summary>
    public async Task<byte[]> Read(string name, PageAddress pageAddress, CancellationToken cancellationToken)
    {
        var data = new byte[PageData.Size];

        await ReadInto(name, pageAddress, data, cancellationToken);

        return data;
    }

    public async Task ReadInto(string name,
                               PageAddress pageAddress,
                               byte[] buffer,
                               CancellationToken cancellationToken)
    {
        var pageCommand = $@"
    EXEC ('DBCC PAGE({name}, {pageAddress.FileId}, {pageAddress.PageId}, {DbccPageHexDumpOption}) WITH TABLERESULTS')
    WITH RESULT SETS
    (
        (
            Unused0 NVARCHAR(4000)
           ,Unused1 NVARCHAR(4000)
           ,Unused2 NVARCHAR(4000)
           ,Value   NVARCHAR(MAX)
        )
    );
";

        Logger.LogDebug("Reading page {PageAddress}: {CommandSql}", pageAddress, pageCommand);

        var offset = 0;

        var start = Stopwatch.GetTimestamp();

        try
        {
            await using var connection = new SqlConnection(ConnectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(pageCommand, connection);

            command.CommandType = CommandType.Text;

            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess,
                                                                      cancellationToken);

            if (reader.HasRows)
            {
                // Reused across every row: prefix (skipped) plus the hex characters we consume
                var valueBuffer = new char[HexLinePrefixLength + HexLineLength];

                while (reader.Read())
                {
                    var charsRead = (int)reader.GetChars(ValueIndex, 0, valueBuffer, 0, valueBuffer.Length);

                    var line = valueBuffer.AsSpan(0, charsRead);

                    if (!IsHexDumpLine(line))
                    {
                        continue;
                    }

                    offset = ReadData(line[HexLinePrefixLength..], offset, buffer);
                }

                reader.Close();
#pragma warning restore VSTHRD103
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error reading page {PageAddress}: {Command} ", pageAddress, pageCommand);

            throw new Exception($"Error reading page {pageAddress.FileId}:{pageAddress.PageId}", ex);
        }

        Logger.LogDebug("Page loaded in {Duration}", Stopwatch.GetElapsedTime(start));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
}
