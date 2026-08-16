using InternalsViewer.Execution.AccessPaths.Memory;
using InternalsViewer.Execution.Tests.Helpers;

namespace InternalsViewer.Execution.Tests.IntegrationTests.Memory;

/// <summary>
/// Measures the memory a sort and a hash match report against the memory the model in <see cref="RowMemory"/> gives them
/// </summary>
/// <remarks>
/// An investigation rather than a check, so nothing here asserts a size. Each case moves one thing - the row count, the row width, the
/// column count, the build side - so the difference between the two figures can be read as a slope rather than a number.
/// </remarks>
public class MemoryGrantInvestigationTests(ITestOutputHelper output)
{
    private const string ConnectionName = "local";

    private const string Options = "OPTION (MAXDOP 1, RECOMPILE)";

    [RequiresConnectionStringFact(ConnectionName)]
    public async Task Sort_Memory_Against_The_Plan_Grant()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(ConnectionName);

        if (await LivePlan.ReachAsync(connectionString) is { } unreachable)
        {
            output.WriteLine($"Not measured: {unreachable}");

            return;
        }

        output.WriteLine("Sort: one row is Id (int) plus a CHAR(width) padded from Id");
        output.WriteLine("");

        WriteHeader();

        foreach (var rows in new[] { 1_000, 5_000, 20_000, 50_000, 100_000 })
        {
            await MeasureSortAsync(connectionString, rows, width: 50, columns: 1);
        }

        foreach (var width in new[] { 10, 100, 200, 400 })
        {
            await MeasureSortAsync(connectionString, rows: 20_000, width, columns: 1);
        }

        foreach (var columns in new[] { 2, 5, 10 })
        {
            await MeasureSortAsync(connectionString, rows: 20_000, width: 100, columns);
        }
    }

    [RequiresConnectionStringFact(ConnectionName)]
    public async Task Hash_Match_Memory_Against_The_Plan_Grant()
    {
        var connectionString = ConnectionStringHelper.GetConnectionString(ConnectionName);

        if (await LivePlan.ReachAsync(connectionString) is { } unreachable)
        {
            output.WriteLine($"Not measured: {unreachable}");

            return;
        }

        output.WriteLine("Hash match: the build side is a CHAR(width) padded row, the probe side reads the same table");
        output.WriteLine("");

        WriteHeader();

        foreach (var buildRows in new[] { 1_000, 5_000, 20_000, 50_000 })
        {
            await MeasureHashAsync(connectionString, buildRows, width: 50);
        }

        foreach (var width in new[] { 10, 100, 200 })
        {
            await MeasureHashAsync(connectionString, buildRows: 20_000, width);
        }
    }

    private async Task MeasureSortAsync(string connectionString, int rows, int width, int columns)
    {
        var sql = $"""
                   SELECT x.*
                   FROM (SELECT TOP ({rows}) Id, {PaddedColumns(width, columns)}
                         FROM dbo.ClusteredTable) AS x
                   ORDER BY {string.Join(", ", Enumerable.Range(1, columns).Select(c => $"x.Pad{c}"))}
                   {Options}
                   """;

        var plan = await LivePlan.RunAsync(connectionString, sql);

        var sort = LivePlan.MemoryOperators(plan, "Sort").FirstOrDefault();

        if (sort is null)
        {
            output.WriteLine($"rows {rows}, width {width}, columns {columns}: no sort in the plan");

            return;
        }

        var rowWidths = new List<int> { 4 };

        rowWidths.AddRange(Enumerable.Repeat(width, columns));

        var model = RowMemory.ForSort(sort.ActualRows * RowMemory.SizeOf(rowWidths), sort.ActualRows);

        WriteRow($"sort rows={rows} width={width} cols={columns}", sort.ActualRows, model, sort.GrantedKb, sort.UsedKb);
    }

    private async Task MeasureHashAsync(string connectionString, int buildRows, int width)
    {
        // The build row has to be read by the query, or its columns are projected away and the width being measured never reaches the table
        var sql = $"""
                   SELECT MAX(b.Pad1)
                   FROM (SELECT TOP ({buildRows}) Id, {PaddedColumns(width, 1)}
                         FROM dbo.ClusteredTable) AS b
                        INNER HASH JOIN
                        (SELECT Id FROM dbo.ClusteredTable) AS p
                        ON p.Id = b.Id
                   {Options}
                   """;

        var plan = await LivePlan.RunAsync(connectionString, sql);

        var hash = LivePlan.MemoryOperators(plan, "Hash Match").FirstOrDefault();

        if (hash is null)
        {
            output.WriteLine($"build {buildRows}, width {width}: no hash match in the plan");

            return;
        }

        var (buildActual, _) = LivePlan.ChildRows(plan, hash.NodeId, childIndex: 0);

        var model = RowMemory.ForHashTable(buildActual * RowMemory.SizeOf([4, width]), buildActual);

        WriteRow($"hash build={buildRows} width={width}", buildActual, model, hash.GrantedKb, hash.UsedKb);
    }

    private static string PaddedColumns(int width, int columns)
        => string.Join(", ",
                       Enumerable.Range(1, columns)
                                 .Select(c => $"CAST(RIGHT(REPLICATE('0', {width}) + CAST(Id + {c} AS VARCHAR(10)), {width}) " +
                                              $"AS CHAR({width})) AS Pad{c}"));

    private void WriteHeader()
    {
        output.WriteLine($"{"case",-42} {"rows",8} {"model KB",10} {"rows KB",9} {"over KB",9} " +
                         $"{"granted KB",11} {"used KB",9} {"used/model",11}");
    }

    private void WriteRow(string label, long rows, BufferMemory model, long? grantedKb, long? usedKb)
    {
        var ratio = usedKb is { } used && model.TotalKb > 0 ? (used / model.PagedKb).ToString("N2") : "-";

        output.WriteLine($"{label,-42} {rows,8:N0} {model.PagedKb,10:N0} {model.RowBytes / 1024D,9:N1} " +
                         $"{model.OverheadBytes / 1024D,9:N1} {grantedKb?.ToString("N0") ?? "-",11} " +
                         $"{usedKb?.ToString("N0") ?? "-",9} {ratio,11}");
    }
}
