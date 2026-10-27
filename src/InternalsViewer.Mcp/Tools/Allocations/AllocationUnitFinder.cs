using System.Text.RegularExpressions;
using InternalsViewer.Internals.Engine.Database;
using ModelContextProtocol;

namespace InternalsViewer.Mcp.Tools.Allocations;

public static class AllocationUnitFinder
{
    public static AllocationUnit Find(IReadOnlyDictionary<long, AllocationUnit> units, string unit)
    {
        if (long.TryParse(unit, out var id))
        {
            return units.GetValueOrDefault(id)
                   ?? throw new McpException($"No allocation unit {id}. Use allocations_objects to list them.");
        }

        var pattern = new Regex(unit, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var matches = units.Values.Where(u => pattern.IsMatch(Describe(u))).ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new McpException($"No allocation unit matches '{unit}'. Use allocations_objects to list them."),
            _ => throw new McpException($"'{unit}' matches {matches.Count} allocation units: " +
                                        string.Join(", ", matches.Take(10).Select(u => $"{u.AllocationUnitId} {Describe(u)}")) +
                                        ". Pass an allocation unit id.")
        };
    }

    public static string Describe(AllocationUnit unit)
    {
        var parts = new[] { unit.SchemaName, unit.TableName, unit.IndexName }.Where(p => !string.IsNullOrEmpty(p));

        var name = string.Join(".", parts);

        return name.Length > 0 ? name : unit.DisplayName;
    }
}
