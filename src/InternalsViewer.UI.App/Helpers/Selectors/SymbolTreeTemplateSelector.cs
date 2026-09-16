using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

/// <summary>
/// Picks the module, class, member or clear-exclusions row template for a node of the symbol search tree
/// </summary>
/// <remarks>
/// The tree is built from <see cref="TreeViewNode"/>s rather than bound to the rows, as a template rooted in a
/// TreeViewItem is the container itself and a recycled one carries its bindings to a row of another type.
/// </remarks>
public sealed class SymbolTreeTemplateSelector : DataTemplateSelector
{
    public DataTemplate ModuleTemplate { get; set; } = null!;

    public DataTemplate GroupTemplate { get; set; } = null!;

    public DataTemplate MemberTemplate { get; set; } = null!;

    public DataTemplate ExclusionsTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item) =>
        item switch
        {
            TreeViewNode { Content: SymbolModuleRow } => ModuleTemplate,
            TreeViewNode { Content: SymbolGroupRow } => GroupTemplate,
            TreeViewNode { Content: SymbolExclusionsRow } => ExclusionsTemplate,
            _ => MemberTemplate
        };

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
