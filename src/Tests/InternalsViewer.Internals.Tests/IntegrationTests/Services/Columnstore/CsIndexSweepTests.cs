using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using InternalsViewer.Internals.Tests.Helpers;
using InternalsViewer.Internals.Tests.IntegrationTests.Providers.Metadata;

namespace InternalsViewer.Internals.Tests.IntegrationTests.Services.Columnstore;

/// <summary>
/// Sweeps a database with DBCC CSINDEX looking for header shapes the lab has never produced
/// </summary>
/// <remarks>
/// CSINDEX needs nothing from our own metadata reader, only ids the catalog views give, so it reads a database
/// whose system tables we cannot parse. The object has to be resident in the columnstore object pool, which is
/// what the touch before each dump is for.
/// </remarks>
public sealed class CsIndexSweepTests(ITestOutputHelper testOutput) : ProviderTestBase(testOutput)
{
    private static readonly string[] Databases = ["WideWorldImportersDW", "AdventureWorksDW2025"];

    private static readonly int[] KnownStructureTypes = [3, 7];

    private static readonly int[] KnownSubLobTypes = [0, 1, 2, 4, 5, 6, 8, 9];

    private static readonly int[] KnownEncodings = [1, 2, 3, 4, 5];

    private static readonly int[] KnownDictionaryTypes = [1, 3, 4];

    private static readonly HashSet<string> KnownHeadings =
    [
        "RLE Header", "Bookmark Header", "VLD Header", "Page Size Array Header", "Page Size Array Data",
        "Bitpack Header", "Bitpack Data Header", "Dictionary Header", "HashTable Header", "Array Header",
        "String Store Header", "String Handle Array Header", "VLD Page Header", "Array Data", "RLE Data",
        "Bookmark Data", "Bitpack Data", "Handle Array Data", "String Data", "Segment Header",
        "HashTable Array Data", "String Handle Array Data"
    ];

    private readonly List<string> _lines = [];

    private readonly List<string> _findings = [];

    private readonly SortedSet<string> _seen = [];

    private readonly SortedSet<string> _headings = [];

    private bool _dumped;

    [RequiresConnectionStringFact("local")]
    public async Task Sweep_For_Unknown_Combinations()
    {
        foreach (var databaseName in Databases)
        {
            _seen.Clear();

            _headings.Clear();

            _findings.Clear();

            _dumped = false;

            _lines.Add(string.Empty);

            _lines.Add($"================ {databaseName} ================");

            await Sweep(databaseName);
        }

        Write();
    }

    private async Task Sweep(string DatabaseName)
    {
        var builder = new SqlConnectionStringBuilder(ConnectionStringHelper.GetConnectionString("local"))
        {
            InitialCatalog = DatabaseName,
            ConnectTimeout = 30
        };

        var messages = new List<string>();

        await using var connection = new SqlConnection(builder.ConnectionString);

        connection.FireInfoMessageEventOnUserErrors = true;

        connection.InfoMessage += (_, e) => messages.Add(e.Message);

        try
        {
            await connection.OpenAsync();
        }
        catch (Exception exception)
        {
            _lines.Add($"cannot open {DatabaseName}: {exception.Message}");

            return;
        }

        _lines.Add($"version {await Scalar(connection, "SELECT @@VERSION")}");

        _lines.Add($"compatibility {await Scalar(connection, "SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID()")}");

        var databaseId = Convert.ToInt32(await Scalar(connection, "SELECT DB_ID()"));

        await Execute(connection, "DBCC TRACEON(3604) WITH NO_INFOMSGS");

        var targets = await GetTargets(connection);

        _lines.Add($"{targets.Count} segment targets");

        await using (var command = new SqlCommand("""
                                                  SELECT o.name + '.' + c.name, TYPE_NAME(c.user_type_id),
                                                         c.max_length, s.encoding_type, COUNT(*)
                                                  FROM sys.column_store_segments s
                                                  JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                  JOIN sys.objects o ON o.object_id = p.object_id
                                                  JOIN sys.columns c ON c.object_id = p.object_id AND c.column_id = s.column_id
                                                  WHERE s.encoding_type IN (4, 5)
                                                  GROUP BY o.name, c.name, c.user_type_id, c.max_length, s.encoding_type
                                                  ORDER BY s.encoding_type, o.name
                                                  """, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                _lines.Add($"  store by value: {reader.GetValue(0)} {reader.GetValue(1)}({reader.GetValue(2)}) "
                           + $"enc {reader.GetValue(3)} segments {reader.GetValue(4)}");
            }
        }

        foreach (var target in targets)
        {
            Flag($"{target.Label} encoding", target.Encoding, KnownEncodings);

            messages.Clear();

            try
            {
                await Execute(connection, $"SELECT TOP (1) * FROM {target.Table} WITH (NOLOCK)");

                await Execute(connection,
                              $"DBCC CSINDEX({databaseId}, {target.Hobt}, {target.Column + 1}, {target.RowGroup}, 1, 0)");
            }
            catch (Exception exception)
            {
                _lines.Add($"{target.Label}: {exception.Message}");

                continue;
            }

            Inspect(target.Label, messages);
        }

        foreach (var dictionary in await GetDictionaries(connection))
        {
            Flag("dictionary type", dictionary.Type, KnownDictionaryTypes);

            messages.Clear();

            try
            {
                await Execute(connection, $"SELECT TOP (1) * FROM {dictionary.Table} WITH (NOLOCK)");

                // Kind 2 reads a global dictionary and kind 4 a local one, the id meaning a dictionary either way
                var kind = dictionary.Dictionary == 0 ? 2 : 4;

                await Execute(connection,
                              $"DBCC CSINDEX({databaseId}, {dictionary.Hobt}, {dictionary.Column + 1}, "
                              + $"{dictionary.Dictionary}, {kind}, 0)");
            }
            catch (Exception exception)
            {
                _lines.Add($"{dictionary.Label}: {exception.Message}");

                continue;
            }

            Inspect(dictionary.Label, messages);

            if (_dumped || !messages.Any(m => m.Contains("HashTable Array Data")))
            {
                continue;
            }

            _dumped = true;

            _lines.Add($"--- FULL DUMP {dictionary.Label} ---");

            foreach (var line in messages.SelectMany(m => m.Split(Environment.NewLine))
                                         .Select(m => m.TrimEnd())
                                         .Where(m => m.Trim().Length > 0)
                                         .Take(40))
            {
                _lines.Add($"  {line}");
            }

            _lines.Add("--- END DUMP ---");
        }

        _lines.Add(string.Empty);

        _lines.Add($"HEADINGS SEEN: {string.Join(" | ", _headings)}");

        _lines.Add(string.Empty);

        _lines.Add($"VALUES SEEN: {string.Join(" | ", _seen)}");

        _lines.Add(string.Empty);

        _lines.Add(_findings.Count == 0 ? "NO UNKNOWN COMBINATIONS FOUND" : $"{_findings.Count} findings");

        _lines.AddRange(_findings);
    }

    /// <summary>
    /// Reads the header values CSINDEX prints, keeping an inventory as well as anything outside what the lab has
    /// </summary>
    private void Inspect(string label, List<string> messages)
    {
        var text = string.Join(Environment.NewLine, messages);

        foreach (var name in new[] { "SubLob Type", "Structure Type", "Lobtype", "Version", "Compression",
                                     "Flags", "Common Size", "Encoding Type", "Bitpack Entry Size" })
        {
            foreach (Match match in Regex.Matches(text, $@"{Regex.Escape(name)}\s*=\s*(-?\d+)"))
            {
                var value = int.Parse(match.Groups[1].Value);

                _seen.Add($"{name} = {value}");

                if (name == "SubLob Type")
                {
                    Flag($"sub lob type", value, KnownSubLobTypes);
                }

                if (name == "Structure Type")
                {
                    Flag($"structure type", value, KnownStructureTypes);
                }
            }
        }

        // A CSINDEX message can carry several lines, so a heading is a line rather than a whole message
        foreach (var heading in messages.SelectMany(m => m.Split(Environment.NewLine))
                                        .Select(m => m.Trim())
                                        .Where(m => m.Length is > 0 and < 40
                                                    && (m.EndsWith("Header") || m.EndsWith("Header:")
                                                        || m.EndsWith("Data") || m.EndsWith("Data:"))))
        {
            var trimmed = heading.TrimEnd(':');

            _headings.Add(trimmed);

            if (!KnownHeadings.Contains(trimmed))
            {
                Flag($"heading '{trimmed}' first on {label}", -1, []);
            }
        }
    }

    private void Flag(string label, int value, int[] known)
    {
        var finding = value < 0 ? $"NEW {label}" : $"NEW {label} {value}";

        if (!known.Contains(value) && !_findings.Contains(finding))
        {
            _findings.Add(finding);
        }
    }

    private async Task<List<(long Hobt, int Column, int RowGroup, int Encoding, string Table, string Label)>> GetTargets(
        SqlConnection connection)
    {
        var targets = new List<(long, int, int, int, string, string)>();

        await using var command = new SqlCommand("""
                                                 SELECT s.hobt_id, s.column_id, s.segment_id, s.encoding_type,
                                                        QUOTENAME(SCHEMA_NAME(o.schema_id)) + '.' + QUOTENAME(o.name),
                                                        o.name + '.' + c.name + ' rg' + CAST(s.segment_id AS varchar(10))
                                                        + ' enc' + CAST(s.encoding_type AS varchar(10))
                                                 FROM sys.column_store_segments s
                                                 JOIN sys.partitions p ON p.partition_id = s.hobt_id
                                                 JOIN sys.objects o ON o.object_id = p.object_id
                                                 JOIN sys.columns c ON c.object_id = p.object_id AND c.column_id = s.column_id
                                                 WHERE s.segment_id = 0
                                                 ORDER BY o.name, s.column_id
                                                 """, connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            targets.Add((reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
                         reader.GetString(4), reader.GetString(5)));
        }

        return targets;
    }

    private async Task<List<(long Hobt, int Column, int Dictionary, int Type, string Table, string Label)>> GetDictionaries(
        SqlConnection connection)
    {
        var targets = new List<(long, int, int, int, string, string)>();

        await using var command = new SqlCommand("""
                                                 SELECT d.hobt_id, d.column_id, d.dictionary_id, d.type,
                                                        QUOTENAME(SCHEMA_NAME(o.schema_id)) + '.' + QUOTENAME(o.name),
                                                        o.name + '.' + c.name + ' dict' + CAST(d.dictionary_id AS varchar(10))
                                                        + ' type' + CAST(d.type AS varchar(10))
                                                 FROM sys.column_store_dictionaries d
                                                 JOIN sys.partitions p ON p.partition_id = d.hobt_id
                                                 JOIN sys.objects o ON o.object_id = p.object_id
                                                 JOIN sys.columns c ON c.object_id = p.object_id AND c.column_id = d.column_id
                                                 ORDER BY o.name, d.column_id, d.dictionary_id
                                                 """, connection);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            targets.Add((reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
                         reader.GetString(4), reader.GetString(5)));
        }

        return targets;
    }

    private void Write()
    {
        ProbeDump.Write("csindex_sweep.txt", _lines);

        foreach (var line in _lines)
        {
            TestOutput.WriteLine(line);
        }
    }

    private static async Task<object?> Scalar(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);

        return await command.ExecuteScalarAsync();
    }

    private static async Task Execute(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };

        await command.ExecuteNonQueryAsync();
    }
}
