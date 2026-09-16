using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

/// <summary>
/// One module in the symbol search results, with the classes found in it beneath
/// </summary>
public sealed partial class SymbolModuleRow(string module, IReadOnlyList<SymbolGroupRow> classes) : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    public string Module => module;

    public IReadOnlyList<SymbolGroupRow> Classes => classes;

    public string CountLabel => $"({classes.Sum(c => c.Members.Count)})";
}
