using InternalsViewer.UI.App.Models.Query.CallStack.Values;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Helpers.Selectors;

public sealed class ValueTreeTemplateSelector : DataTemplateSelector
{
    public DataTemplate TypeTemplate { get; set; } = null!;

    public DataTemplate GroupTemplate { get; set; } = null!;

    public DataTemplate FunctionTemplate { get; set; } = null!;

    public DataTemplate CallTemplate { get; set; } = null!;

    public DataTemplate NoteTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item) =>
        item switch
        {
            TreeViewNode { Content: ValueTypeRow } => TypeTemplate,
            TreeViewNode { Content: ValueUseGroup } => GroupTemplate,
            TreeViewNode { Content: ValueUseRow } => FunctionTemplate,
            TreeViewNode { Content: ValueCallRow } => CallTemplate,
            _ => NoteTemplate
        };

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
