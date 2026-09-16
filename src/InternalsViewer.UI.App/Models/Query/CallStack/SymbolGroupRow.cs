using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

/// <summary>
/// One class of one module in the symbol search results, with the members found under it
/// </summary>
public sealed partial class SymbolGroupRow(string module, string className, IReadOnlyList<ClassMemberRow> members, string highlight)
    : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    public string Module => module;

    public string ClassName => className;

    public IReadOnlyList<ClassMemberRow> Members => members;

    public string ModulePrefix => $"{module}!";

    public string Label => className.Length > 0 ? className : "(no class)";

    public string CountLabel => $"({members.Count})";

    public string Highlight => highlight;
}
