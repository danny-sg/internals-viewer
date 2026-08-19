using System.Collections;
using System.Data.SqlTypes;
using System.Globalization;
using InternalsViewer.Internals.Columnstore.Decoding;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Interfaces.Services.Loaders.Engine;
using InternalsViewer.Internals.Tests.VerificationTool.Helpers;
using InternalsViewer.Internals.Tests.VerificationTool.Models;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Internals.Tests.VerificationTool.Services;

/// <summary>
/// Verifies decoded columnstore row groups against the values the engine returns
/// </summary>
/// <remarks>
/// Row order within a row group is not the table order and the engine will not expose a row ordinal, so a column
/// holding unique values anchors the comparison - decoding it identifies which queried row each ordinal holds.
/// </remarks>
internal class ColumnstoreVerificationService(ColumnstoreService columnstoreService, IDatabaseService databaseService)
    : VerificationService(databaseService)
{
    private ColumnstoreService ColumnstoreService { get; } = columnstoreService;

    public async Task VerifyColumnstore(string databaseName, string tableName, string keyColumnName)
    {
        var database = await CreateDatabase(databaseName);

        var results = new List<VerificationResult>();

        foreach (var allocationUnit in FindTables(database, tableName))
        {
            results.AddRange(await VerifyTable(databaseName, database, allocationUnit.TableName!, keyColumnName));
        }

        WriteMessage($"Verification complete. {results.Count} row group(s)");

        WriteSuccess($"{results.Sum(r => r.PassCount)} passed");

        var failed = results.Sum(r => r.FailCount);

        if (failed > 0)
        {
            WriteError($"{failed} failed");
        }
        else
        {
            WriteSuccess("0 failed");
        }
    }

    private async Task<List<VerificationResult>> VerifyTable(string databaseName,
                                                             DatabaseSource database,
                                                             string tableName,
                                                             string keyColumnName)
    {
        var results = new List<VerificationResult>();

        var allocationUnit = database.AllocationUnits.Values.First(a => a.TableName == tableName);

        var index = await ColumnstoreService.GetIndex(allocationUnit, database, CancellationToken.None);

        var compressed = index.CompressedRowGroups.ToList();

        if (compressed.Count == 0)
        {
            WriteMessage($"{tableName}: no compressed row groups");

            return results;
        }

        foreach (var rowGroup in compressed)
        {
            results.Add(await VerifyRowGroup(databaseName, database, tableName, keyColumnName, rowGroup));
        }

        return results;
    }

    private async Task<VerificationResult> VerifyRowGroup(string databaseName,
                                                          DatabaseSource database,
                                                          string tableName,
                                                          string keyColumnName,
                                                          RowGroup rowGroup)
    {
        var result = new VerificationResult();

        var reader = await ColumnstoreService.GetRowGroupReader(database, rowGroup, CancellationToken.None);

        var keyIndex = reader.GetColumnIndex(keyColumnName);

        if (keyIndex < 0)
        {
            WriteError($"{tableName} row group {rowGroup.RowGroupId}: key column {keyColumnName} is not readable");

            result.FailCount++;

            return result;
        }

        var columnNames = reader.Columns.Select(c => c?.Name ?? string.Empty).ToList();

        var expected = await QueryRows(databaseName, tableName, columnNames, keyColumnName);

        foreach (var segment in reader.Skipped)
        {
            WriteMessage($"{tableName} row group {rowGroup.RowGroupId}: skipped column "
                         + $"{segment.Column?.Name ?? segment.Key.ColumnId.ToString()} ({segment.Encoding})");
        }

        for (var ordinal = 0; ordinal < reader.RowCount; ordinal++)
        {
            var row = reader.GetRow(ordinal);

            var key = Normalise(row[keyIndex]);

            if (key is null || !expected.TryGetValue(key, out var queried))
            {
                WriteError($"{tableName} row group {rowGroup.RowGroupId} ordinal {ordinal}: "
                           + $"{keyColumnName} {row[keyIndex]} not found in the table");

                result.FailCount++;

                continue;
            }

            var mismatches = Compare(row, queried, columnNames);

            if (mismatches.Count == 0)
            {
                result.PassCount++;

                continue;
            }

            result.FailCount++;

            if (result.FailCount <= MaximumReportedFailures)
            {
                foreach (var mismatch in mismatches)
                {
                    WriteError($"{tableName} row group {rowGroup.RowGroupId} ordinal {ordinal} "
                               + $"{keyColumnName} {key}: {mismatch}");
                }
            }
        }

        WriteMessage($"{tableName} row group {rowGroup.RowGroupId}: {result.PassCount:N0} passed, "
                     + $"{result.FailCount:N0} failed over {columnNames.Count} column(s)");

        return result;
    }

    private const int MaximumReportedFailures = 20;

    private static List<string> Compare(object?[] decoded, object?[] queried, List<string> columnNames)
    {
        var mismatches = new List<string>();

        for (var i = 0; i < decoded.Length; i++)
        {
            var left = Normalise(decoded[i]);

            var right = Normalise(queried[i]);

            if (Equals(left, right))
            {
                continue;
            }

            mismatches.Add($"{columnNames[i]} decoded [{Describe(decoded[i])}] queried [{Describe(queried[i])}]");
        }

        return mismatches;
    }

    /// <summary>
    /// Reduces both sides to a comparable form, since the decoder works from storage types and the client from the
    /// column types
    /// </summary>
    private static object? Normalise(object? value) => value switch
    {
        null or DBNull => null,
        byte[] bytes => System.Convert.ToHexString(bytes),
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        TimeOnly time => time.ToTimeSpan(),
        bool flag => flag ? 1M : 0M,
        float single => Math.Round((decimal)single, 4),
        double @double => Math.Round((decimal)@double, 4),
        SqlDecimal sqlDecimal => TrimScale(sqlDecimal.ToString()),
        decimal @decimal => TrimScale(@decimal.ToString(CultureInfo.InvariantCulture)),
        byte or short or int or long => System.Convert.ToDecimal(value),
        string text => text.TrimEnd(),
        _ => value
    };

    /// <summary>
    /// Drops trailing zeros so the same number compares equal whichever scale it was rendered at
    /// </summary>
    private static string TrimScale(string value)
        => value.Contains('.') ? value.TrimEnd('0').TrimEnd('.') : value;

    private static string Describe(object? value) => value switch
    {
        null or DBNull => "null",
        byte[] bytes => System.Convert.ToHexString(bytes),
        IEnumerable and not string => string.Join(",", ((IEnumerable)value).Cast<object>()),
        _ => value.ToString() ?? string.Empty
    };

    private static async Task<Dictionary<object, object?[]>> QueryRows(string databaseName,
                                                                      string tableName,
                                                                      List<string> columnNames,
                                                                      string keyColumnName)
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(databaseName);

        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();

        var columns = string.Join(", ", columnNames.Select(c => $"[{c}]"));

        await using var command = new SqlCommand($"SELECT {columns} FROM [{tableName}]", connection)
        {
            CommandTimeout = 0
        };

        await using var reader = await command.ExecuteReaderAsync();

        var keyOrdinal = columnNames.FindIndex(c => string.Equals(c, keyColumnName, StringComparison.OrdinalIgnoreCase));

        var rows = new Dictionary<object, object?[]>();

        while (await reader.ReadAsync())
        {
            var values = new object?[columnNames.Count];

            reader.GetValues(values!);

            var key = Normalise(values[keyOrdinal]);

            if (key is not null)
            {
                rows[key] = values;
            }
        }

        return rows;
    }

    private static IEnumerable<AllocationUnit> FindTables(DatabaseSource database, string tableName)
        => database.AllocationUnits
                   .Values
                   .Where(a => a.TableName is not null
                               && (tableName == "*" || string.Equals(a.TableName, tableName, StringComparison.OrdinalIgnoreCase)))
                   .GroupBy(a => a.TableName)
                   .Select(g => g.First());
}
