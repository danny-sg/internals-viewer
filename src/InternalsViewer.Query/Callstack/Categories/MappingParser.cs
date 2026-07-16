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

        foreach (var cells in ReadRows(reader, minCells: 4))
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
                DefinitionOrder = order++,
            };
        }
    }

    /// <summary>
    /// Parses the operator file: which plan operator a frame belongs to, and what to badge it
    /// </summary>
    /// <remarks>
    /// minCells is 4, not 5: it is a minimum and a row below it is dropped in silence, so requiring the fifth would
    /// delete every rule that stated only a badge and lost its trailing pipe. Both cells are optional anyway — a rule
    /// states one, the other, or both.
    ///
    /// The header row has no cell that fails to parse (unlike the other two files, where the category catches it), so it
    /// is dropped by name.
    /// </remarks>
    public static IEnumerable<OperatorRule> ParseOperators(TextReader reader, int startOrder = 0)
    {
        var order = startOrder;

        foreach (var cells in ReadRows(reader, minCells: 4))
        {
            if (string.Equals(cells[0], "Module", StringComparison.OrdinalIgnoreCase)
                && string.Equals(cells[3], "Iterator", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new OperatorRule
            {
                Module = GlobPattern.Parse(cells[0]),
                Class = GlobPattern.Parse(cells[1]),
                Function = GlobPattern.Parse(cells[2]),
                Iterator = Optional(cells, 3),
                PlanOperator = PlanOperators(Optional(cells, 4)),
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

    /// <summary>
    /// Parses the barrier file: the frames a unit of storage work begins at
    /// </summary>
    /// <remarks>
    /// The header row survives <see cref="ReadRows"/> like any other and cannot be told from a rule by parsing alone —
    /// there is no category cell here to fail on, as there is for the other two files — so it is dropped by name.
    /// </remarks>
    public static IEnumerable<FramePattern> ParseBarriers(TextReader reader)
    {
        foreach (var cells in ReadRows(reader, minCells: 3))
        {
            if (string.Equals(cells[0], "Module", StringComparison.OrdinalIgnoreCase)
                && string.Equals(cells[1], "Class", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new FramePattern
            {
                Module = GlobPattern.Parse(cells[0]),
                Class = GlobPattern.Parse(cells[1]),
                Function = GlobPattern.Parse(cells[2]),
            };
        }
    }

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
