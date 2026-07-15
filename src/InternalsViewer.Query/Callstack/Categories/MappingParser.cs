namespace InternalsViewer.Query.CallStack.Categories;

/// <summary>
/// Parses the pipe-delimited mapping files into module entries and symbol rules
/// </summary>
/// <remarks>
/// Blank lines, <c>#</c> comments and the header row are skipped; cells are trimmed. A row whose category cell is not a
/// known enum name (a bad row, or the header) is skipped rather than throwing, so one malformed line can't break loading.
/// </remarks>
public static class MappingParser
{
    public static IEnumerable<(string Module, ModuleCategory Category)> ParseModules(TextReader reader)
    {
        foreach (var cells in ReadRows(reader, minCells: 2))
        {
            if (Enum.TryParse<ModuleCategory>(cells[1], ignoreCase: true, out var category))
            {
                yield return (cells[0], category);
            }
        }
    }

    public static IEnumerable<SymbolCategoryRule> ParseSymbols(TextReader reader, int startOrder = 0)
    {
        var order = startOrder;

        // minCells stays at 5 though a rule has six fields: it is a minimum and a row below it is dropped silently, so
        // requiring the sixth would delete every five-cell rule (an override file, a row missing its trailing pipe)
        // without a word. A row that stops at the Iterator simply names no plan operator, which is the common case.
        foreach (var cells in ReadRows(reader, minCells: 5))
        {
            if (!Enum.TryParse<SymbolCategory>(cells[3], ignoreCase: true, out var category))
            {
                continue;
            }

            yield return new SymbolCategoryRule
            {
                Module = GlobPattern.Parse(cells[0]),
                Class = GlobPattern.Parse(cells[1]),
                Function = GlobPattern.Parse(cells[2]),
                Category = category,
                Iterator = Optional(cells, 4),
                PlanOperator = PlanOperators(Optional(cells, 5)),
                DefinitionOrder = order++,
            };
        }
    }

    // Blank is empty, not GlobPattern.Parse("") — an empty pattern is match-anything, which for the plan operator would
    // make every unmapped frame an operator boundary.
    private static IReadOnlyList<GlobPattern> PlanOperators(string? cell)
        => cell is null
            ? []
            : [.. cell.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Select(GlobPattern.Parse)];

    private static string? Optional(string[] cells, int index)
        => index < cells.Length && !string.IsNullOrWhiteSpace(cells[index]) ? cells[index] : null;

    private static IEnumerable<string[]> ReadRows(TextReader reader, int minCells)
    {
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var cells = line.Split('|');

            if (cells.Length < minCells)
            {
                continue;
            }

            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = cells[i].Trim();
            }

            yield return cells;
        }
    }
}
