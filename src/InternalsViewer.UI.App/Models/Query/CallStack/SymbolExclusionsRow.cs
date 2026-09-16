using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

/// <summary>
/// The last row of the symbol search results while modules are excluded, offering to search them again
/// </summary>
public sealed class SymbolExclusionsRow(IReadOnlyList<string> modules)
{
    public string Label => "Clear exclusions";

    public string Detail => $"Excluded: {string.Join(", ", modules)}";
}
