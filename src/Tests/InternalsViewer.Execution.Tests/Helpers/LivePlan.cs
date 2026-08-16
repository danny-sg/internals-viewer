using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.Execution.Tests.Helpers;

/// <summary>
/// One memory consuming operator as the actual plan reported it
/// </summary>
internal sealed record PlanOperatorMemory(string PhysicalOperator,
                                          int NodeId,
                                          long ActualRows,
                                          double EstimateRows,
                                          long? GrantedKb,
                                          long? UsedKb);

/// <summary>
/// Runs a query against a live server and reads the actual plan back, which is where the memory an operator used is reported
/// </summary>
internal static class LivePlan
{
    /// <summary>
    /// Whether the server is there, so a measurement can report that it did not run rather than fail
    /// </summary>
    public static async Task<string?> ReachAsync(string connectionString)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);

            await connection.OpenAsync();

            return null;
        }
        catch (SqlException exception)
        {
            return exception.Message.Split(Environment.NewLine)[0];
        }
    }

    public static async Task<XDocument> RunAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);

        await connection.OpenAsync();

        await using (var statisticsOn = new SqlCommand("SET STATISTICS XML ON", connection))
        {
            await statisticsOn.ExecuteNonQueryAsync();
        }

        string? plan = null;

        await using (var command = new SqlCommand(sql, connection) { CommandTimeout = 300 })
        {
            await using var reader = await command.ExecuteReaderAsync();

            do
            {
                while (await reader.ReadAsync())
                {
                    if (reader.FieldCount == 1 && reader.GetValue(0) is string text && text.Contains("ShowPlanXML"))
                    {
                        plan = text;
                    }
                }
            }
            while (await reader.NextResultAsync());
        }

        if (plan is null)
        {
            throw new InvalidOperationException("The query returned no actual execution plan");
        }

        return XDocument.Parse(plan);
    }

    /// <summary>
    /// The memory consuming operators of a plan, in the order the plan lists them
    /// </summary>
    public static List<PlanOperatorMemory> MemoryOperators(XDocument plan, params string[] physicalOperators)
    {
        var wanted = new HashSet<string>(physicalOperators, StringComparer.OrdinalIgnoreCase);

        return [.. plan.Descendants()
                       .Where(e => e.Name.LocalName == "RelOp")
                       .Where(e => wanted.Contains(Attribute(e, "PhysicalOp") ?? string.Empty))
                       .Select(Read)];
    }

    /// <summary>
    /// Rows the child of an operator actually returned, which for a hash match's first child is the build side
    /// </summary>
    public static (long ActualRows, double EstimateRows) ChildRows(XDocument plan, int nodeId, int childIndex)
    {
        var operators = plan.Descendants()
                            .Where(e => e.Name.LocalName == "RelOp")
                            .ToList();

        var parent = operators.Single(e => Attribute(e, "NodeId") == nodeId.ToString());

        var children = parent.Descendants()
                             .Where(e => e.Name.LocalName == "RelOp")
                             .Where(e => e.Ancestors().First(a => a.Name.LocalName == "RelOp") == parent)
                             .ToList();

        var child = children[childIndex];

        return (ActualRowsOf(child), double.Parse(Attribute(child, "EstimateRows") ?? "0"));
    }

    private static PlanOperatorMemory Read(XElement relOp)
    {
        return new PlanOperatorMemory(Attribute(relOp, "PhysicalOp") ?? string.Empty,
                                      int.Parse(Attribute(relOp, "NodeId") ?? "-1"),
                                      ActualRowsOf(relOp),
                                      double.Parse(Attribute(relOp, "EstimateRows") ?? "0"),
                                      SumCounters(relOp, "GrantedMemoryKb"),
                                      SumCounters(relOp, "UsedMemoryGrant") ?? SumCounters(relOp, "UsedMemoryKb"));
    }

    private static long ActualRowsOf(XElement relOp)
        => SumCounters(relOp, "ActualRows") ?? 0;

    /// <summary>
    /// Totals a runtime counter over the threads that ran the operator, taking only the counters the operator owns
    /// </summary>
    private static long? SumCounters(XElement relOp, string name)
    {
        var counters = relOp.Elements()
                            .Where(e => e.Name.LocalName == "RunTimeInformation")
                            .SelectMany(e => e.Elements().Where(c => c.Name.LocalName == "RunTimeCountersPerThread"))
                            .ToList();

        if (counters.Count == 0)
        {
            return null;
        }

        var values = counters.Select(c => Attribute(c, name))
                             .OfType<string>()
                             .Select(long.Parse)
                             .ToList();

        return values.Count == 0 ? null : values.Sum();
    }

    private static string? Attribute(XElement element, string name)
        => element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value;
}
